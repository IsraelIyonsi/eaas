using System.Security.Claims;
using EaaS.Api.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EaaS.Api.Tests.Authentication;

public sealed class AdminRbacTests
{
    private static async Task<bool> EvaluatePolicyAsync(string policyName, string role)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("SuperAdminPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("AdminRole", "SuperAdmin");
            });

            options.AddPolicy("AdminPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("AdminRole", "SuperAdmin", "Admin");
            });

            options.AddPolicy("AdminReadPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("AdminRole", "SuperAdmin", "Admin", "ReadOnly");
            });
        });

        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        var claims = new[] { new Claim("AdminRole", role) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var result = await authService.AuthorizeAsync(principal, policyName);
        return result.Succeeded;
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("Admin", false)]
    [InlineData("ReadOnly", false)]
    public async Task SuperAdminPolicy_Should_OnlyAllowSuperAdmin(string role, bool expected)
    {
        var result = await EvaluatePolicyAsync("SuperAdminPolicy", role);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("Admin", true)]
    [InlineData("ReadOnly", false)]
    public async Task AdminPolicy_Should_AllowSuperAdminAndAdmin(string role, bool expected)
    {
        var result = await EvaluatePolicyAsync("AdminPolicy", role);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("Admin", true)]
    [InlineData("ReadOnly", true)]
    public async Task AdminReadPolicy_Should_AllowAllRoles(string role, bool expected)
    {
        var result = await EvaluatePolicyAsync("AdminReadPolicy", role);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task SuperAdminPolicy_Should_DenyUnauthenticatedUser()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("SuperAdminPolicy", policy =>
            {
                policy.AuthenticationSchemes.Add(AdminSessionAuthHandler.SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("AdminRole", "SuperAdmin");
            });
        });

        var sp = services.BuildServiceProvider();
        var authService = sp.GetRequiredService<IAuthorizationService>();

        // Unauthenticated: no authentication type
        var claims = new[] { new Claim("AdminRole", "SuperAdmin") };
        var identity = new ClaimsIdentity(claims); // no auth type = unauthenticated
        var principal = new ClaimsPrincipal(identity);

        var result = await authService.AuthorizeAsync(principal, "SuperAdminPolicy");
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AdminPolicy_Should_DenyUnknownRole()
    {
        var result = await EvaluatePolicyAsync("AdminPolicy", "Viewer");
        result.Should().BeFalse();
    }
}
