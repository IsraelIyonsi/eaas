namespace EaaS.Shared.Constants;

public static class EmailConstants
{
    public const string MessageIdPrefix = "eaas_";
    public const string BatchIdPrefix = "batch_";
    public const int BatchShortIdLength = 8;
    public const int MaxRecipientsPerEmail = 50;
    public const int MaxBatchSize = 100;
    public const int MaxTags = 10;
    public const int MaxTagLength = 50;
    public const int MaxIdempotencyKeyLength = 255;
    public const int MaxTemplateNameLength = 100;
}
