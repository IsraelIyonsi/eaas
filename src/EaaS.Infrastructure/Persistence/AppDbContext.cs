using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SendingDomain> Domains => Set<SendingDomain>();
    public DbSet<DnsRecord> DnsRecords => Set<DnsRecord>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<Email> Emails => Set<Email>();
    public DbSet<EmailEvent> EmailEvents => Set<EmailEvent>();
    public DbSet<SuppressionEntry> SuppressionEntries => Set<SuppressionEntry>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Register PostgreSQL enums
        modelBuilder.HasPostgresEnum<EmailStatus>();
        modelBuilder.HasPostgresEnum<EventType>();
        modelBuilder.HasPostgresEnum<DomainStatus>();
        modelBuilder.HasPostgresEnum<ApiKeyStatus>();
        modelBuilder.HasPostgresEnum<SuppressionReason>();
        modelBuilder.HasPostgresEnum<DnsRecordPurpose>();

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
