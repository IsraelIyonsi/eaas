using System.Security.Cryptography;
using AngleSharp;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using EaaS.Domain.Entities;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Services;

public sealed partial class ClickTrackingLinkRewriter
{
    private readonly AppDbContext _dbContext;
    private readonly string _baseUrl;
    private readonly ILogger<ClickTrackingLinkRewriter> _logger;

    public ClickTrackingLinkRewriter(
        AppDbContext dbContext,
        IOptions<TrackingSettings> settings,
        ILogger<ClickTrackingLinkRewriter> logger)
    {
        _dbContext = dbContext;
        _baseUrl = settings.Value.BaseUrl.TrimEnd('/');
        _logger = logger;
    }

    public async Task<string> RewriteLinksAsync(string htmlBody, Guid emailId, CancellationToken cancellationToken)
    {
        var context = BrowsingContext.New(AngleSharp.Configuration.Default);
        var parser = context.GetService<IHtmlParser>()!;
        var document = parser.ParseDocument(htmlBody);

        var anchors = document.QuerySelectorAll("a[href]");
        var trackingLinks = new List<TrackingLink>();

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
                continue;

            // Skip mailto:, anchor (#), and unsubscribe links
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith('#')
                || href.Contains("unsubscribe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Scheme allowlist: only http(s) absolute URLs may be persisted and rewritten.
            // Anything else (javascript:, data:, file:, vbscript:, relative, malformed) is
            // left untouched so it cannot be laundered through our click endpoint.
            if (!Uri.TryCreate(href, UriKind.Absolute, out var parsedUri)
                || !(string.Equals(parsedUri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(parsedUri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
            {
                var scheme = parsedUri?.Scheme ?? "(unparseable)";
                LogDisallowedScheme(_logger, scheme, emailId);
                continue;
            }

            // Generate a short random token for the lookup table
            var token = GenerateShortToken();

            var trackingLink = new TrackingLink
            {
                Id = Guid.NewGuid(),
                EmailId = emailId,
                Token = token,
                OriginalUrl = href,
                CreatedAt = DateTime.UtcNow
            };

            trackingLinks.Add(trackingLink);

            anchor.SetAttribute("href", $"{_baseUrl}/track/click/{token}");
        }

        if (trackingLinks.Count > 0)
        {
            _dbContext.TrackingLinks.AddRange(trackingLinks);
            await _dbContext.SaveChangesAsync(cancellationToken);
            LogLinksRewritten(_logger, trackingLinks.Count, emailId);
        }

        // Serialize back to HTML
        return document.DocumentElement.OuterHtml;
    }

    private static string GenerateShortToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rewrote {Count} links for click tracking on EmailId={EmailId}")]
    private static partial void LogLinksRewritten(ILogger logger, int count, Guid emailId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped link rewrite for disallowed scheme '{Scheme}' on EmailId={EmailId}")]
    private static partial void LogDisallowedScheme(ILogger logger, string scheme, Guid emailId);
}
