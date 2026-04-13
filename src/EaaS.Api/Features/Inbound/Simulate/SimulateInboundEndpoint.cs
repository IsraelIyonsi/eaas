using System.Text.Json;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

using EaaS.Api.Constants;
namespace EaaS.Api.Features.Inbound.Simulate;

/// <summary>
/// Local development endpoint that simulates an inbound email arriving.
/// Bypasses SES/S3/SNS — creates the InboundEmail record directly.
/// Only available in Development environment.
/// </summary>
public static class SimulateInboundEndpoint
{
    public sealed record SimulateInboundRequest(
        string FromEmail,
        string? FromName,
        List<string> To,
        List<string>? Cc,
        string Subject,
        string? HtmlBody,
        string? TextBody,
        string? InReplyTo
    );

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/simulate", async (
            SimulateInboundRequest request,
            HttpContext httpContext,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var tenantId = Guid.Parse(
                httpContext.User.FindFirst(ClaimNameConstants.TenantId)?.Value ?? Guid.Empty.ToString());

            // Resolve domain from recipient
            var recipientDomain = request.To.FirstOrDefault()?.Split('@').LastOrDefault();
            if (string.IsNullOrEmpty(recipientDomain))
                return Results.BadRequest(ApiErrorResponse.Create("INVALID_RECIPIENT", "At least one To address is required."));

            var domain = await dbContext.Domains
                .AsNoTracking()
                .FirstOrDefaultAsync(d =>
                    d.TenantId == tenantId &&
                    d.DomainName == recipientDomain &&
                    d.DeletedAt == null,
                    cancellationToken);

            if (domain is null)
                return Results.BadRequest(ApiErrorResponse.Create("DOMAIN_NOT_FOUND",
                    $"No verified domain found for '{recipientDomain}'. Add a domain first."));

            var emailId = Guid.NewGuid();
            var messageId = $"<sim-{emailId:N}@{recipientDomain}>";

            var toEmails = request.To.Select(e => new { email = e, name = "" }).ToList();
            var ccEmails = (request.Cc ?? new List<string>()).Select(e => new { email = e, name = "" }).ToList();

            // Check for reply tracking
            Guid? outboundEmailId = null;
            if (!string.IsNullOrEmpty(request.InReplyTo))
            {
                var outbound = await dbContext.Emails
                    .AsNoTracking()
                    .Where(e => e.TenantId == tenantId && e.MessageId == request.InReplyTo)
                    .Select(e => new { e.Id })
                    .FirstOrDefaultAsync(cancellationToken);

                outboundEmailId = outbound?.Id;
            }

            var inboundEmail = new InboundEmail
            {
                Id = emailId,
                TenantId = tenantId,
                MessageId = messageId,
                FromEmail = request.FromEmail,
                FromName = request.FromName,
                ToEmails = JsonSerializer.Serialize(toEmails),
                CcEmails = JsonSerializer.Serialize(ccEmails),
                Subject = request.Subject,
                HtmlBody = request.HtmlBody,
                TextBody = request.TextBody,
                Headers = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["From"] = request.FromName != null ? $"{request.FromName} <{request.FromEmail}>" : request.FromEmail,
                    ["To"] = string.Join(", ", request.To),
                    ["Subject"] = request.Subject,
                    ["Message-ID"] = messageId,
                    ["Date"] = DateTime.UtcNow.ToString("R"),
                }),
                Status = InboundEmailStatus.Processed,
                SpamVerdict = "pass",
                VirusVerdict = "pass",
                SpfVerdict = "pass",
                DkimVerdict = "pass",
                DmarcVerdict = "pass",
                InReplyTo = request.InReplyTo,
                OutboundEmailId = outboundEmailId,
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };

            dbContext.InboundEmails.Add(inboundEmail);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Match rules
            var matchedRule = await dbContext.InboundRules
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId && r.IsActive)
                .OrderBy(r => r.Priority)
                .ToListAsync(cancellationToken);

            string? matchedRuleName = null;
            foreach (var rule in matchedRule)
            {
                if (rule.MatchPattern == "*@" || rule.MatchPattern == "*")
                {
                    matchedRuleName = rule.Name;
                    break;
                }
                if (rule.MatchPattern.EndsWith('@') &&
                    request.To.Any(t => t.StartsWith(rule.MatchPattern, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedRuleName = rule.Name;
                    break;
                }
            }

            return Results.Created($"/api/v1/inbound/emails/{emailId}", ApiResponse.Ok(new
            {
                id = emailId,
                messageId,
                fromEmail = request.FromEmail,
                to = request.To,
                subject = request.Subject,
                status = "processed",
                matchedRule = matchedRuleName,
                outboundEmailId,
                receivedAt = inboundEmail.ReceivedAt,
            }));
        })
        .WithName("SimulateInboundEmail")
        .Produces<ApiResponse<object>>(StatusCodes.Status201Created);
    }
}
