using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.Messaging;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;

namespace EaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool includeMassTransit = true,
        bool publishOnly = false)
    {
        // Bind settings
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));
        services.Configure<SesSettings>(configuration.GetSection(SesSettings.SectionName));
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.Configure<RateLimitingSettings>(configuration.GetSection(RateLimitingSettings.SectionName));

        // PostgreSQL with NpgsqlDataSourceBuilder (Gate 2 fix: MapEnum via data source builder)
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("PostgreSQL connection string is required.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<EmailStatus>();
        dataSourceBuilder.MapEnum<EventType>();
        dataSourceBuilder.MapEnum<DomainStatus>();
        dataSourceBuilder.MapEnum<ApiKeyStatus>();
        dataSourceBuilder.MapEnum<SuppressionReason>();
        dataSourceBuilder.MapEnum<DnsRecordPurpose>();
        dataSourceBuilder.MapEnum<InboundEmailStatus>();
        dataSourceBuilder.MapEnum<InboundRuleAction>();
        dataSourceBuilder.MapEnum<AdminRole>();
        dataSourceBuilder.MapEnum<TenantStatus>();
        dataSourceBuilder.MapEnum<AuditAction>();
        dataSourceBuilder.MapEnum<PaymentProvider>();
        dataSourceBuilder.MapEnum<PlanTier>();
        dataSourceBuilder.MapEnum<SubscriptionStatus>();
        dataSourceBuilder.MapEnum<InvoiceStatus>();
        dataSourceBuilder.MapEnum<WebhookStatus>();
        dataSourceBuilder.MapEnum<DnsRecordType>();

        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                npgsqlOptions.MapEnum<EmailStatus>();
                npgsqlOptions.MapEnum<EventType>();
                npgsqlOptions.MapEnum<DomainStatus>();
                npgsqlOptions.MapEnum<ApiKeyStatus>();
                npgsqlOptions.MapEnum<SuppressionReason>();
                npgsqlOptions.MapEnum<DnsRecordPurpose>();
                npgsqlOptions.MapEnum<InboundEmailStatus>();
                npgsqlOptions.MapEnum<InboundRuleAction>();
                npgsqlOptions.MapEnum<AdminRole>();
                npgsqlOptions.MapEnum<TenantStatus>();
                npgsqlOptions.MapEnum<AuditAction>();
                npgsqlOptions.MapEnum<PaymentProvider>();
                npgsqlOptions.MapEnum<PlanTier>();
                npgsqlOptions.MapEnum<SubscriptionStatus>();
                npgsqlOptions.MapEnum<InvoiceStatus>();
                npgsqlOptions.MapEnum<WebhookStatus>();
                npgsqlOptions.MapEnum<DnsRecordType>();
            }));

        // Redis
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddSingleton<RedisCacheService>();
        services.AddSingleton<ISuppressionCache>(sp => sp.GetRequiredService<RedisCacheService>());
        services.AddSingleton<IRateLimiter>(sp => sp.GetRequiredService<RedisCacheService>());
        services.AddSingleton<IApiKeyCache>(sp => sp.GetRequiredService<RedisCacheService>());
        services.AddSingleton<IIdempotencyStore>(sp => sp.GetRequiredService<RedisCacheService>());
        services.AddSingleton<ITemplateCache>(sp => sp.GetRequiredService<RedisCacheService>());

        // Subscription limit service
        services.AddScoped<ISubscriptionLimitService, SubscriptionLimitService>();

        // Tracking services
        services.Configure<TrackingSettings>(configuration.GetSection(TrackingSettings.SectionName));
        services.AddSingleton<ITrackingTokenService, TrackingTokenService>();
        services.AddScoped<TrackingPixelInjector>();
        services.AddScoped<ClickTrackingLinkRewriter>();

        // List-Unsubscribe + CAN-SPAM footer injection
        services.Configure<ListUnsubscribeSettings>(configuration.GetSection(ListUnsubscribeSettings.SectionName));
        services.AddSingleton<ListUnsubscribeService>();
        services.AddSingleton<EmailFooterInjector>();

        // MassTransit with RabbitMQ
        if (includeMassTransit && publishOnly)
            services.AddMassTransitPublishOnly();
        else if (includeMassTransit)
            services.AddMassTransitWithRabbitMq();

        return services;
    }

    public static IServiceCollection AddInboundServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<InboundSettings>(configuration.GetSection(InboundSettings.SectionName));

        var inboundSettings = configuration.GetSection(InboundSettings.SectionName).Get<InboundSettings>()
            ?? new InboundSettings();
        var sesSettings = configuration.GetSection(SesSettings.SectionName).Get<SesSettings>()
            ?? new SesSettings();

        services.AddSingleton<Amazon.S3.IAmazonS3>(_ =>
            new Amazon.S3.AmazonS3Client(
                sesSettings.AccessKeyId,
                sesSettings.SecretAccessKey,
                Amazon.RegionEndpoint.GetBySystemName(inboundSettings.S3Region)));

        services.AddSingleton<IInboundEmailStorage, S3InboundEmailStorage>();
        services.AddSingleton<IInboundEmailParser, MimeKitInboundParser>();

        return services;
    }

    public static IServiceCollection AddEmailProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var emailProvider = configuration["EMAIL_PROVIDER"] ?? "ses";
        if (string.Equals(emailProvider, "smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<SmtpEmailService>();
            services.AddSingleton<IDomainIdentityService>(sp => sp.GetRequiredService<SmtpEmailService>());
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<SmtpEmailService>());
        }
        else
        {
            var sesSettings = configuration.GetSection(SesSettings.SectionName).Get<SesSettings>() ?? new SesSettings();
            services.AddSingleton<Amazon.SimpleEmailV2.IAmazonSimpleEmailServiceV2>(_ =>
                new Amazon.SimpleEmailV2.AmazonSimpleEmailServiceV2Client(
                    sesSettings.AccessKeyId,
                    sesSettings.SecretAccessKey,
                    Amazon.RegionEndpoint.GetBySystemName(sesSettings.Region)));
            services.AddSingleton<SesEmailService>();
            services.AddSingleton<IDomainIdentityService>(sp => sp.GetRequiredService<SesEmailService>());
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<SesEmailService>());
        }

        return services;
    }
}
