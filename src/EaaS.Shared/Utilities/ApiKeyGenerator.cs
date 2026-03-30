using System.Security.Cryptography;
using System.Text;
using EaaS.Shared.Constants;

namespace EaaS.Shared.Utilities;

public static class ApiKeyGenerator
{
    public static string GenerateKey()
    {
        var random = new char[ApiKeyConstants.RandomPartLength];

        for (var i = 0; i < ApiKeyConstants.RandomPartLength; i++)
            random[i] = ApiKeyConstants.AllowedCharacters[RandomNumberGenerator.GetInt32(ApiKeyConstants.AllowedCharacters.Length)];

        return ApiKeyConstants.LiveKeyPrefix + new string(random);
    }

    public static string ComputeSha256Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
