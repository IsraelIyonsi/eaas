using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using EaaS.Api.Constants;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.Authentication;

/// <summary>
/// Convention tests guarding the admin route prefix contract.
///
/// The dashboard BFF only signs proxy tokens for requests whose path starts with
/// <c>/api/v1/admin/</c>. If an admin-authenticated endpoint were mounted at a
/// different prefix, the API would still accept the unsigned <c>X-Admin-User-Id</c>
/// header from a non-proxy caller (during grace) or reject it in confusing ways.
///
/// These tests enforce the invariant at build time rather than relying on an
/// integration spin-up of the full web host.
/// </summary>
public sealed class AdminRoutePrefixConventionTests
{
    private const string AdminPrefix = "/api/v1/admin/";

    /// <summary>
    /// Every <c>RouteConstants</c> field whose name starts with <c>Admin</c>
    /// must resolve to a path under <c>/api/v1/admin/</c>. Customer-facing
    /// routes are intentionally kept OUT of that prefix so the dashboard BFF
    /// cannot accidentally sign proxy tokens for them.
    /// </summary>
    [Fact]
    public void AdminRouteConstants_ShouldAllStartWithAdminPrefix()
    {
        var adminFields = typeof(RouteConstants)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Where(f => f.Name.StartsWith("Admin", System.StringComparison.Ordinal))
            .ToList();

        adminFields.Should().NotBeEmpty(
            "RouteConstants should expose at least one Admin* route for the admin surface");

        foreach (var field in adminFields)
        {
            var value = (string)field.GetRawConstantValue()!;
            var withSlash = value.EndsWith('/') ? value : value + "/";
            withSlash.StartsWith(AdminPrefix, System.StringComparison.OrdinalIgnoreCase)
                .Should()
                .BeTrue(
                    $"RouteConstants.{field.Name} = \"{value}\" must live under \"{AdminPrefix}\" "
                    + "so the dashboard proxy can sign requests to it");
        }
    }

    /// <summary>
    /// Source-level convention check on <c>EndpointMappingExtensions.cs</c>:
    /// every <c>MapGroup(...)</c> that calls <c>.RequireAuthorization(...)</c>
    /// with an admin policy constant (<c>AdminPolicy</c>, <c>SuperAdminPolicy</c>,
    /// or <c>AdminReadPolicy</c>) must use a <c>RouteConstants.Admin*</c> path.
    ///
    /// This catches the failure mode where a new admin group is wired to a
    /// non-admin route constant (e.g. <c>RouteConstants.Emails</c>) and the
    /// dashboard proxy silently drops the signed token.
    /// </summary>
    [Fact]
    public void AdminAuthenticatedGroups_ShouldOnlyUseAdminRouteConstants()
    {
        var source = ReadEndpointMappingSource();

        // Match: MapGroup(RouteConstants.<Name>)   ...   .RequireAuthorization(AuthorizationPolicyConstants.<Policy>)
        // Tolerate whitespace/newlines between the two calls within a single
        // chained statement (the `.RequireAuthorization` that follows the
        // same `MapGroup` call).
        var pattern = new Regex(
            @"MapGroup\s*\(\s*RouteConstants\.(?<route>\w+)\s*\)" +
            @"(?<chain>[\s\S]{0,400}?)" +
            @"\.RequireAuthorization\s*\(\s*AuthorizationPolicyConstants\.(?<policy>\w+)",
            RegexOptions.Compiled);

        var adminPolicies = new[]
        {
            AuthorizationPolicyConstants.AdminPolicy,
            AuthorizationPolicyConstants.SuperAdminPolicy,
            AuthorizationPolicyConstants.AdminReadPolicy,
        };

        var matches = pattern.Matches(source);
        matches.Should().NotBeEmpty(
            "EndpointMappingExtensions should contain at least one admin-authorized group");

        var violations = new System.Collections.Generic.List<string>();
        var adminGroupCount = 0;

        foreach (Match match in matches)
        {
            var policy = match.Groups["policy"].Value;
            if (!adminPolicies.Contains(policy))
            {
                continue;
            }

            adminGroupCount++;
            var routeName = match.Groups["route"].Value;
            var routeField = typeof(RouteConstants).GetField(
                routeName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            routeField.Should().NotBeNull(
                $"RouteConstants.{routeName} must exist (referenced in EndpointMappingExtensions)");

            var routeValue = (string)routeField!.GetRawConstantValue()!;
            var withSlash = routeValue.EndsWith('/') ? routeValue : routeValue + "/";

            if (!withSlash.StartsWith(AdminPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(
                    $"MapGroup(RouteConstants.{routeName} = \"{routeValue}\") is guarded by "
                    + $"AuthorizationPolicyConstants.{policy} but does NOT sit under "
                    + $"\"{AdminPrefix}\". The dashboard BFF will not sign proxy tokens for this path.");
            }
        }

        adminGroupCount.Should().BeGreaterThan(0,
            "at least one MapGroup must be guarded by an admin policy");
        violations.Should().BeEmpty(
            "all admin-authorized MapGroup calls must use a RouteConstants.Admin* path. "
            + "Violations:\n" + string.Join("\n", violations));
    }

    private static string ReadEndpointMappingSource()
    {
        // Walk upward from the test assembly location to find the repo root,
        // then read the source file directly. The file is checked in and
        // stable — no build artifacts involved.
        var dir = new DirectoryInfo(
            Path.GetDirectoryName(typeof(AdminRoutePrefixConventionTests).Assembly.Location)!);

        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src", "EaaS.Api", "Extensions", "EndpointMappingExtensions.cs");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/EaaS.Api/Extensions/EndpointMappingExtensions.cs "
            + "by walking up from the test assembly location.");
    }
}
