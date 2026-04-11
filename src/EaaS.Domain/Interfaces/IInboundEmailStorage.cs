namespace EaaS.Domain.Interfaces;

public interface IInboundEmailStorage
{
    Task<string> StoreRawEmailAsync(Guid tenantId, Guid emailId, Stream mimeStream, CancellationToken ct);
    Task<Stream> GetRawEmailAsync(string s3Key, CancellationToken ct);
    Task<string> StoreAttachmentAsync(Guid tenantId, Guid emailId, string filename, Stream content, CancellationToken ct);
    Task<Stream> GetAttachmentAsync(string s3Key, CancellationToken ct);
    Task DeleteEmailAsync(string s3Key, CancellationToken ct);
}
