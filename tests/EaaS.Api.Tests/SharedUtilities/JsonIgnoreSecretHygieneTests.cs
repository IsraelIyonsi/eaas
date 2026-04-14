using System.Text.Json;
using EaaS.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EaaS.Api.Tests.SharedUtilities;

/// <summary>
/// Guard tests for Tranche 2 G1 (H1-H3): sensitive fields on domain entities must be
/// decorated with [JsonIgnore] so they never leak through accidental serialization
/// (logs, error responses, debug dumps, etc.).
/// </summary>
public sealed class JsonIgnoreSecretHygieneTests
{
    [Fact]
    public void Webhook_Serialize_DoesNotExposeSecret()
    {
        const string secret = "whsec_super_sensitive_hmac_key_abc123";
        var webhook = new Webhook
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Url = "https://example.com/hook",
            Events = new[] { "email.delivered" },
            Secret = secret,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var json = JsonSerializer.Serialize(webhook);

        json.Should().NotContain(secret);
        json.Should().NotContain("\"Secret\"", "sensitive HMAC secret must be [JsonIgnore]d");
    }

    [Fact]
    public void Tenant_Serialize_DoesNotExposePasswordHash()
    {
        const string hash = "$argon2id$v=19$m=65536,t=3,p=4$abcdef1234567890";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corp",
            PasswordHash = hash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var json = JsonSerializer.Serialize(tenant);

        json.Should().NotContain(hash);
        json.Should().NotContain("\"PasswordHash\"", "password hash must be [JsonIgnore]d");
    }

    [Fact]
    public void AdminUser_Serialize_DoesNotExposePasswordHash()
    {
        const string hash = "$argon2id$v=19$m=65536,t=3,p=4$adminhashvalue12345";
        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            DisplayName = "Admin",
            PasswordHash = hash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var json = JsonSerializer.Serialize(admin);

        json.Should().NotContain(hash);
        json.Should().NotContain("\"PasswordHash\"", "admin password hash must be [JsonIgnore]d");
    }
}
