using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Services;

public sealed class TrackingPixelInjector
{
    private readonly ITrackingTokenService _tokenService;
    private readonly string _baseUrl;

    public TrackingPixelInjector(ITrackingTokenService tokenService, IOptions<TrackingSettings> settings)
    {
        _tokenService = tokenService;
        _baseUrl = settings.Value.BaseUrl.TrimEnd('/');
    }

    public string InjectTrackingPixel(string htmlBody, Guid emailId)
    {
        var token = _tokenService.GenerateToken(emailId, "open");
        var pixelTag = $"<img src=\"{_baseUrl}/track/open/{token}\" width=\"1\" height=\"1\" style=\"display:none\" alt=\"\" />";

        // Inject before </body> if present, otherwise append
        var bodyCloseIndex = htmlBody.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyCloseIndex >= 0)
        {
            return htmlBody.Insert(bodyCloseIndex, pixelTag);
        }

        return htmlBody + pixelTag;
    }
}
