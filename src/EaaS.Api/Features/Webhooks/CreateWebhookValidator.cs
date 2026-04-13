using System.Net;
using FluentValidation;

namespace EaaS.Api.Features.Webhooks;

public sealed class CreateWebhookValidator : AbstractValidator<CreateWebhookCommand>
{
    private static readonly string[] ValidEvents =
        { "queued", "sent", "delivered", "bounced", "complained", "opened", "clicked", "failed" };

    public CreateWebhookValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.")
            .MaximumLength(2048).WithMessage("URL must not exceed 2048 characters.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            .WithMessage("URL must be a valid HTTPS URL.")
            .Must(url => !IsPrivateOrLoopbackUrl(url))
            .WithMessage("URL must not point to a private or loopback IP address.");

        RuleFor(x => x.Events)
            .NotEmpty().WithMessage("At least one event type is required.")
            .Must(events => events.All(e => ValidEvents.Contains(e.ToLowerInvariant())))
            .WithMessage($"Events must be one of: {string.Join(", ", ValidEvents)}.");
    }

    private static bool IsPrivateOrLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host;

        if (!IPAddress.TryParse(host, out var ip))
            return false; // hostname — allow it (DNS rebinding is an accepted limitation)

        if (IPAddress.IsLoopback(ip))
            return true;

        // Map IPv6-mapped IPv4 to plain IPv4 for range checks
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false; // non-IPv4, non-loopback — allow

        var bytes = ip.GetAddressBytes();

        // 10.0.0.0/8
        if (bytes[0] == 10)
            return true;

        // 172.16.0.0/12  (172.16.x.x – 172.31.x.x)
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;

        // 169.254.0.0/16 (link-local / AWS metadata)
        if (bytes[0] == 169 && bytes[1] == 254)
            return true;

        return false;
    }
}
