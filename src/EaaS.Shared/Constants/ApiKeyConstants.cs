namespace EaaS.Shared.Constants;

public static class ApiKeyConstants
{
    public const string LiveKeyPrefix = "eaas_live_";
    public const int RandomPartLength = 40;
    public const string AllowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    public const int MaxNameLength = 100;
    public const int GracePeriodHours = 24;
}
