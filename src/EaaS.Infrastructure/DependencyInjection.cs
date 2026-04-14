using EaaS.Domain.Enums;
using EaaS.Domain.Interfaces;
using EaaS.Domain.Providers;
using EaaS.Infrastructure.Configuration;
using EaaS.Infrastructure.EmailProviders;
using EaaS.Infrastructure.EmailProviders.Configuration;
using EaaS.Infrastructure.EmailProviders.Providers.Ses;
using EaaS.Infrastructure.EmailProviders.Providers.Smtp;
using EaaS.Infrastructure.Messaging;
using EaaS.Infrastructure.Persistence;
using EaaS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        // PostgreSQL
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

        // Password reset
        services.Configure<PasswordResetSettings>(configuration.GetSection(PasswordResetSettings.SectionName));
        services.AddSingleton<IPasswordResetTokenStore, RedisPasswordResetTokenStore>();
        services.AddSingleton<PasswordResetTokenService>();
        services.AddSingleton<IPasswordResetEmailSender, PasswordResetEmailSender>();

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

    /// <summary>
    /// Registers the <see cref="IEmailProvider"/> abstraction (Phase 0):
    /// <list type="bullet">
    /// <item>Binds <see cref="SesOptions"/> from <c>EmailProviders:Ses</c> with validation.</item>
    /// <item>Registers the SES and SMTP adapters keyed by <see cref="EmailProviderConfigKeys.ProviderKeys"/>.</item>
    /// <item>Registers a single shared <see cref="IEmailProviderFactory"/>.</item>
    /// <item>Registers <see cref="IDomainIdentityService"/> pointed at the matching adapter.</item>
    /// <item>Logs the state of <see cref="EmailProviderConfigKeys.FeatureFlag"/> at startup.</item>
    /// </list>
    /// The legacy <c>EMAIL_PROVIDER</c> env var is honoured for back-compat with local-dev compose files.
    /// </summary>
    public static IServiceCollection AddEmailProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Feature flag — read once at startup, logged via hosted service.
        var featureFlagValue = configuration.GetValue<bool?>(EmailProviderConfigKeys.FeatureFlag) ?? true;
        services.AddSingleton(new EmailProviderFeatureFlag(featureFlagValue));
        services.AddHostedService<EmailProviderFeatureFlagLogger>();

        // Strongly-typed options — validated, fail-fast at startup.
        // Bind from the canonical section first, with a fallback read of the legacy "Ses" section
        // so existing appsettings (which use "Ses") continue to work until a separate config migration PR.
        services.AddOptions<SesOptions>()
            .Configure<IConfiguration>((opts, cfg) =>
            {
                var newSection = cfg.GetSection(EmailProviderConfigKeys.Ses.Section);
                if (newSection.Exists())
                {
                    newSection.Bind(opts);
                    return;
                }
                // Fall back to the legacy Ses settings shape.
                var legacy = cfg.GetSection(SesSettings.SectionName).Get<SesSettings>() ?? new SesSettings();
                opts.AccessKeyId = legacy.AccessKeyId;
                opts.SecretAccessKey = legacy.SecretAccessKey;
                opts.Region = legacy.Region;
                opts.ConfigurationSetName = legacy.ConfigurationSetName;
                opts.MaxSendRate = legacy.MaxSendRate;
            })
            .Validate(o =>
                !string.IsNullOrWhiteSpace(o.AccessKeyId)
                && !string.IsNullOrWhiteSpace(o.SecretAccessKey)
                && !string.IsNullOrWhiteSpace(o.Region),
                "SES credentials (AccessKeyId, SecretAccessKey, Region) are required.");

        // Back-compat: the legacy "EMAIL_PROVIDER" env var still selects SMTP (Mailpit) in local dev.
        // When unset (production / staging), we register SES as the default — same behaviour as before.
        var legacyProviderKey = configuration["EMAIL_PROVIDER"];
        var routingDefault = configuration[EmailProviderConfigKeys.Routing.DefaultProvider];
        var defaultKey = routingDefault
            ?? (string.Equals(legacyProviderKey, "smtp", StringComparison.OrdinalIgnoreCase)
                ? EmailProviderConfigKeys.ProviderKeys.Smtp
                : EmailProviderConfigKeys.ProviderKeys.Ses);

        // Register adapters. We always register SMTP (cheap stub) and whatever set includes the default.
        services.AddSingleton<SmtpEmailProvider>();
        services.AddSingleton<SmtpDomainIdentityService>();

        if (string.Equals(defaultKey, EmailProviderConfigKeys.ProviderKeys.Smtp, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailProvider>(sp => sp.GetRequiredService<SmtpEmailProvider>());
            services.AddSingleton<IDomainIdentityService>(sp => sp.GetRequiredService<SmtpDomainIdentityService>());
        }
        else
        {
            // Register the SES adapter.
            services.AddSingleton<Amazon.SimpleEmailV2.IAmazonSimpleEmailServiceV2>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<SesOptions>>().Value;
                return new Amazon.SimpleEmailV2.AmazonSimpleEmailServiceV2Client(
                    opts.AccessKeyId,
                    opts.SecretAccessKey,
                    Amazon.RegionEndpoint.GetBySystemName(opts.Region));
            });

            services.AddSingleton<SesEmailProvider>();
            services.AddSingleton<SesDomainIdentityService>();
            services.AddSingleton<IEmailProvider>(sp => sp.GetRequiredService<SesEmailProvider>());
            services.AddSingleton<IDomainIdentityService>(sp => sp.GetRequiredService<SesDomainIdentityService>());
        }

        // The factory is keyed off the final default-provider string.
        services.AddSingleton<IEmailProviderFactory>(sp =>
            new EmailProviderFactory(
                sp.GetServices<IEmailProvider>(),
                defaultKey,
                sp.GetRequiredService<ILogger<EmailProviderFactory>>()));

        return services;
    }
}

/// <summary>Startup-time record of the email-provider feature-flag value.</summary>
internal sealed record EmailProviderFeatureFlag(bool Enabled);

/// <summary>Logs the feature flag exactly once at startup — runbook §4 requirement.</summary>
internal sealed partial class EmailProviderFeatureFlagLogger : Microsoft.Extensions.Hosting.IHostedService
{
    private readonly EmailProviderFeatureFlag _flag;
    private readonly ILogger<EmailProviderFeatureFlagLogger> _logger;

    public EmailProviderFeatureFlagLogger(EmailProviderFeatureFlag flag, ILogger<EmailProviderFeatureFlagLogger> logger)
    {
        _flag = flag;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogFeatureFlag(_logger, EmailProviderConfigKeys.FeatureFlag, _flag.Enabled);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Feature flag {Key} = {Enabled}")]
    private static partial void LogFeatureFlag(ILogger logger, string key, bool enabled);
}
