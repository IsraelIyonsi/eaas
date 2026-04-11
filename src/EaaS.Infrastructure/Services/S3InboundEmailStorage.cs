using Amazon.S3;
using Amazon.S3.Model;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EaaS.Infrastructure.Services;

public sealed class S3InboundEmailStorage : IInboundEmailStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly InboundSettings _settings;

    public S3InboundEmailStorage(IAmazonS3 s3Client, IOptions<InboundSettings> settings)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
    }

    public async Task<string> StoreRawEmailAsync(Guid tenantId, Guid emailId, Stream mimeStream, CancellationToken ct)
    {
        var key = $"{tenantId}/inbound/{emailId}/raw.eml";

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _settings.S3BucketName,
            Key = key,
            InputStream = mimeStream,
            ContentType = "message/rfc822"
        }, ct);

        return key;
    }

    public async Task<Stream> GetRawEmailAsync(string s3Key, CancellationToken ct)
    {
        var response = await _s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _settings.S3BucketName,
            Key = s3Key
        }, ct);

        return response.ResponseStream;
    }

    public async Task<string> StoreAttachmentAsync(Guid tenantId, Guid emailId, string filename, Stream content, CancellationToken ct)
    {
        var sanitizedFilename = SanitizeFilename(filename);
        var key = $"{tenantId}/inbound/{emailId}/attachments/{sanitizedFilename}";

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _settings.S3BucketName,
            Key = key,
            InputStream = content
        }, ct);

        return key;
    }

    public async Task<Stream> GetAttachmentAsync(string s3Key, CancellationToken ct)
    {
        var response = await _s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _settings.S3BucketName,
            Key = s3Key
        }, ct);

        return response.ResponseStream;
    }

    public async Task DeleteEmailAsync(string s3Key, CancellationToken ct)
    {
        // Delete the raw email and all attachments under the same prefix
        var prefix = s3Key.Replace("/raw.eml", "/");

        var listResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = _settings.S3BucketName,
            Prefix = prefix
        }, ct);

        if (listResponse.S3Objects.Count > 0)
        {
            await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = _settings.S3BucketName,
                Objects = listResponse.S3Objects
                    .Select(o => new KeyVersion { Key = o.Key })
                    .ToList()
            }, ct);
        }
    }

    private static string SanitizeFilename(string filename)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", filename.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }
}
