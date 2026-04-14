using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EaaS.Api.Features.Unsubscribe;

/// <summary>
/// CAN-SPAM §7704(a)(4) + RFC 8058 One-Click unsubscribe endpoint.
/// - GET /u/{token} renders a plain confirmation page (and performs the
///   suppression — this matches typical user expectation when they click
///   the footer link).
/// - POST /u/{token} is the RFC 8058 One-Click endpoint called by the MUA
///   when List-Unsubscribe-Post is present. Returns 200 on success.
/// Both are idempotent and anonymous.
/// </summary>
public static class UnsubscribeEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/u/{token}", async (string token, IMediator mediator) =>
        {
            var result = await mediator.Send(new UnsubscribeCommand(token));
            var html = BuildConfirmationHtml(result);
            return Results.Content(html, "text/html; charset=utf-8",
                statusCode: result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        })
        .AllowAnonymous()
        .WithName("UnsubscribeGet")
        .WithTags("Unsubscribe");

        app.MapPost("/u/{token}", async (string token, IMediator mediator) =>
        {
            var result = await mediator.Send(new UnsubscribeCommand(token));
            return result.Success
                ? Results.Ok(new { unsubscribed = true })
                : Results.BadRequest(new { unsubscribed = false, error = "invalid_token" });
        })
        .AllowAnonymous()
        .WithName("UnsubscribeOneClick")
        .WithTags("Unsubscribe");
    }

    private static string BuildConfirmationHtml(UnsubscribeResult result)
    {
        if (!result.Success)
        {
            return """
                <!doctype html><html><head><meta charset="utf-8"><title>Unsubscribe</title></head>
                <body style="font-family:system-ui,sans-serif;max-width:560px;margin:4rem auto;padding:0 1rem;color:#222">
                <h1>Invalid or expired link</h1>
                <p>We could not verify this unsubscribe link. If you continue to receive unwanted email,
                please reply to the message with the word <strong>UNSUBSCRIBE</strong> and we will remove you.</p>
                </body></html>
                """;
        }

        return """
            <!doctype html><html><head><meta charset="utf-8"><title>Unsubscribed</title></head>
            <body style="font-family:system-ui,sans-serif;max-width:560px;margin:4rem auto;padding:0 1rem;color:#222">
            <h1>You have been unsubscribed</h1>
            <p>You will no longer receive marketing email from this sender.
            This change takes effect immediately.</p>
            <p style="color:#666;font-size:12px">Powered by SendNex — CAN-SPAM §7704(a)(4) / RFC 8058.</p>
            </body></html>
            """;
    }
}
