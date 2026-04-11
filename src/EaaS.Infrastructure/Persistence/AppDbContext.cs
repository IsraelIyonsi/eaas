using EaaS.Domain.Entities;
using EaaS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EaaS.Infrastructure.Persistence;

public class AppDbContext : DbContext
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
    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();
    public DbSet<TrackingLink> TrackingLinks => Set<TrackingLink>();
    public DbSet<InboundEmail> InboundEmails => Set<InboundEmail>();
    public DbSet<InboundAttachment> InboundAttachments => Set<InboundAttachment>();
    public DbSet<InboundRule> InboundRules => Set<InboundRule>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

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
        modelBuilder.HasPostgresEnum<InboundEmailStatus>();
        modelBuilder.HasPostgresEnum<InboundRuleAction>();
        modelBuilder.HasPostgresEnum<AdminRole>();
        modelBuilder.HasPostgresEnum<TenantStatus>();
        modelBuilder.HasPostgresEnum<AuditAction>();
        modelBuilder.HasPostgresEnum<PaymentProvider>();
        modelBuilder.HasPostgresEnum<PlanTier>();
        modelBuilder.HasPostgresEnum<SubscriptionStatus>();
        modelBuilder.HasPostgresEnum<InvoiceStatus>();
        modelBuilder.HasPostgresEnum<WebhookStatus>();
        modelBuilder.HasPostgresEnum<DnsRecordType>();

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
