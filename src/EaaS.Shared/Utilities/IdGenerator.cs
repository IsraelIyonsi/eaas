using System.Security.Cryptography;
using EaaS.Shared.Constants;

namespace EaaS.Shared.Utilities;

public static class IdGenerator
{
    private const string AlphanumericChars = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static string GenerateMessageId()
        => $"{EmailConstants.MessageIdPrefix}{Guid.NewGuid():N}";

    public static string GenerateBatchId()
        => $"{EmailConstants.BatchIdPrefix}{GenerateShortId(EmailConstants.BatchShortIdLength)}";

    public static string GenerateShortId(int length)
    {
        var result = new char[length];
        for (var i = 0; i < length; i++)
            result[i] = AlphanumericChars[RandomNumberGenerator.GetInt32(AlphanumericChars.Length)];
        return new string(result);
    }
}
