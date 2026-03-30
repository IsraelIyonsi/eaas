using System.Security.Cryptography;
using System.Text;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using EaaS.Infrastructure.Persistence;
using EaaS.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Api.Commands;

public static class SeedCommand
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task<int> ExecuteAsync(string[] args, IServiceProvider services)
    {
        if (args.Contains("--api-key"))
            return await SeedApiKeyAsync(services);

        if (args.Contains("--dashboard-password"))
            return SeedDashboardPassword(args);

        Console.Error.WriteLine("Usage: dotnet run -- seed --api-key");
        Console.Error.WriteLine("       dotnet run -- seed --dashboard-password <password>");
        return 1;
    }

    private static async Task<int> SeedApiKeyAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Check if bootstrap key already exists
        var exists = await dbContext.ApiKeys
            .AnyAsync(k => k.Name == "Bootstrap Key" && k.TenantId == DefaultTenantId);

        if (exists)
        {
            Console.Error.WriteLine("ERROR: Bootstrap key already exists. Delete it first if you need a new one.");
            return 1;
        }

        var plaintextKey = GenerateApiKey();
        var keyHash = ComputeSha256Hash(plaintextKey);
        var prefix = plaintextKey[..8];

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            Name = "Bootstrap Key",
            KeyHash = keyHash,
            Prefix = prefix,
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync();

        Console.WriteLine("Bootstrap API key created successfully.");
        Console.WriteLine($"API Key: {plaintextKey}");
        Console.WriteLine();
        Console.WriteLine("IMPORTANT: Save this key now. It will never be shown again.");
        return 0;
    }

    private static int SeedDashboardPassword(string[] args)
    {
        var passwordIndex = Array.IndexOf(args, "--dashboard-password") + 1;

        if (passwordIndex >= args.Length || string.IsNullOrWhiteSpace(args[passwordIndex]))
        {
            Console.Error.WriteLine("ERROR: Password argument is required.");
            Console.Error.WriteLine("Usage: dotnet run -- seed --dashboard-password <password>");
            return 1;
        }

        var password = args[passwordIndex];
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: SecurityConstants.BCryptWorkFactor);

        Console.WriteLine("Dashboard password hash generated.");
        Console.WriteLine($"DASHBOARD_PASSWORD_HASH={hash}");
        Console.WriteLine();
        Console.WriteLine("Add this to your .env file.");
        return 0;
    }

    private static string GenerateApiKey()
    {
        var random = new char[ApiKeyConstants.RandomPartLength];

        for (var i = 0; i < ApiKeyConstants.RandomPartLength; i++)
            random[i] = ApiKeyConstants.AllowedCharacters[RandomNumberGenerator.GetInt32(ApiKeyConstants.AllowedCharacters.Length)];

        return ApiKeyConstants.LiveKeyPrefix + new string(random);
    }

    private static string ComputeSha256Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
