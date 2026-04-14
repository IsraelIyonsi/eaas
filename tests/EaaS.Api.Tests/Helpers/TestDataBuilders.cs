using EaaS.Api.Features.Admin.Auth;
using EaaS.Api.Features.Admin.Tenants;
using EaaS.Api.Features.Admin.Users;
using EaaS.Api.Features.ApiKeys;
using EaaS.Api.Features.Billing.Plans;
using EaaS.Api.Features.Billing.Subscriptions;
using EaaS.Api.Features.CustomerAuth;
using EaaS.Api.Features.Domains;
using EaaS.Api.Features.Emails;
using EaaS.Api.Features.Inbound.Rules;
using EaaS.Api.Features.Suppressions;
using EaaS.Api.Features.Templates;
using EaaS.Api.Features.Webhooks;
using EaaS.Domain.Entities;
using EaaS.Domain.Enums;

namespace EaaS.Api.Tests.Helpers;

public static class TestDataBuilders
{
    private static readonly Guid DefaultTenantId = Guid.NewGuid();
    private static readonly Guid DefaultApiKeyId = Guid.NewGuid();

    public static SendEmailCommandBuilder SendEmail() => new();
    public static CreateApiKeyCommandBuilder CreateApiKey() => new();
    public static AddDomainCommandBuilder AddDomain() => new();
    public static CreateTemplateCommandBuilder CreateTemplate() => new();
    public static EmailEntityBuilder AnEmail() => new();
    public static TemplateEntityBuilder ATemplate() => new();
    public static SendingDomainEntityBuilder ADomain() => new();
    public static CreateInboundRuleCommandBuilder CreateInboundRule() => new();
    public static InboundRuleEntityBuilder AnInboundRule() => new();
    public static InboundEmailEntityBuilder AnInboundEmail() => new();
    public static AdminLoginCommandBuilder AdminLogin() => new();
    public static CreateTenantCommandBuilder CreateTenant() => new();
    public static CreateAdminUserCommandBuilder CreateAdminUser() => new();
    public static AdminUserEntityBuilder AnAdminUser() => new();
    public static TenantEntityBuilder ATenant() => new();
    public static PlanEntityBuilder APlan() => new();
    public static CreatePlanCommandBuilder CreatePlan() => new();
    public static SendBatchCommandBuilder SendBatch() => new();
    public static ScheduleEmailCommandBuilder ScheduleEmail() => new();
    public static CreateSubscriptionCommandBuilder CreateSubscription() => new();
    public static AddSuppressionCommandBuilder AddSuppression() => new();
    public static CreateWebhookCommandBuilder CreateWebhook() => new();
    public static UpdateWebhookCommandBuilder UpdateWebhook() => new();
    public static UpdateInboundRuleCommandBuilder UpdateInboundRule() => new();

    public sealed class SendEmailCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private Guid _apiKeyId = DefaultApiKeyId;
        private string _from = "sender@verified.com";
        private List<string> _to = new() { "recipient@example.com" };
        private List<string>? _cc;
        private List<string>? _bcc;
        private string? _subject = "Test Subject";
        private string? _htmlBody = "<p>Hello</p>";
        private string? _textBody = "Hello";
        private Guid? _templateId;
        private Dictionary<string, object>? _variables;
        private List<string>? _tags;
        private Dictionary<string, string>? _metadata;
        private string? _idempotencyKey;

        public SendEmailCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public SendEmailCommandBuilder WithApiKeyId(Guid apiKeyId) { _apiKeyId = apiKeyId; return this; }
        public SendEmailCommandBuilder WithFrom(string from) { _from = from; return this; }
        public SendEmailCommandBuilder WithTo(List<string> to) { _to = to; return this; }
        public SendEmailCommandBuilder WithCc(List<string>? cc) { _cc = cc; return this; }
        public SendEmailCommandBuilder WithBcc(List<string>? bcc) { _bcc = bcc; return this; }
        public SendEmailCommandBuilder WithSubject(string? subject) { _subject = subject; return this; }
        public SendEmailCommandBuilder WithHtmlBody(string? htmlBody) { _htmlBody = htmlBody; return this; }
        public SendEmailCommandBuilder WithTextBody(string? textBody) { _textBody = textBody; return this; }
        public SendEmailCommandBuilder WithTemplateId(Guid? templateId) { _templateId = templateId; return this; }
        public SendEmailCommandBuilder WithVariables(Dictionary<string, object>? variables) { _variables = variables; return this; }
        public SendEmailCommandBuilder WithTags(List<string>? tags) { _tags = tags; return this; }
        public SendEmailCommandBuilder WithMetadata(Dictionary<string, string>? metadata) { _metadata = metadata; return this; }
        public SendEmailCommandBuilder WithIdempotencyKey(string? key) { _idempotencyKey = key; return this; }

        public SendEmailCommand Build() => new(
            _tenantId, _apiKeyId, _from, _to, _cc, _bcc, _subject,
            _htmlBody, _textBody, _templateId, _variables,
            _tags, _metadata, _idempotencyKey);
    }

    public sealed class CreateApiKeyCommandBuilder
    {
        private string _name = "Test API Key";
        private Guid _tenantId = DefaultTenantId;

        public CreateApiKeyCommandBuilder WithName(string name) { _name = name; return this; }
        public CreateApiKeyCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }

        public CreateApiKeyCommand Build() => new(_name, _tenantId);
    }

    public sealed class AddDomainCommandBuilder
    {
        private string _domainName = "example.com";
        private Guid _tenantId = DefaultTenantId;

        public AddDomainCommandBuilder WithDomainName(string domain) { _domainName = domain; return this; }
        public AddDomainCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }

        public AddDomainCommand Build() => new(_domainName, _tenantId);
    }

    public sealed class CreateTemplateCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private string _name = "Welcome Email";
        private string _subjectTemplate = "Hello {{ name }}";
        private string _htmlTemplate = "<h1>Welcome {{ name }}</h1>";
        private string? _textTemplate = "Welcome {{ name }}";

        public CreateTemplateCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public CreateTemplateCommandBuilder WithName(string name) { _name = name; return this; }
        public CreateTemplateCommandBuilder WithSubjectTemplate(string subject) { _subjectTemplate = subject; return this; }
        public CreateTemplateCommandBuilder WithHtmlTemplate(string htmlTemplate) { _htmlTemplate = htmlTemplate; return this; }
        public CreateTemplateCommandBuilder WithTextTemplate(string? textTemplate) { _textTemplate = textTemplate; return this; }

        public CreateTemplateCommand Build() => new(_tenantId, _name, _subjectTemplate, _htmlTemplate, _textTemplate);
    }

    public sealed class EmailEntityBuilder
    {
        private readonly Email _email = new()
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            ApiKeyId = DefaultApiKeyId,
            MessageId = $"eaas_{Guid.NewGuid():N}",
            FromEmail = "sender@verified.com",
            ToEmails = "[\"recipient@example.com\"]",
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            TextBody = "Hello",
            Status = EmailStatus.Queued,
            Metadata = "{}",
            CreatedAt = DateTime.UtcNow
        };

        public EmailEntityBuilder WithId(Guid id) { _email.Id = id; return this; }
        public EmailEntityBuilder WithTenantId(Guid tenantId) { _email.TenantId = tenantId; return this; }
        public EmailEntityBuilder WithTemplateId(Guid? templateId) { _email.TemplateId = templateId; return this; }
        public EmailEntityBuilder WithStatus(EmailStatus status) { _email.Status = status; return this; }

        public Email Build() => _email;
    }

    public sealed class TemplateEntityBuilder
    {
        private readonly Template _template = new()
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            Name = "Welcome Email",
            SubjectTemplate = "Hello {{ name }}",
            HtmlBody = "<h1>Welcome {{ name }}</h1>",
            TextBody = "Welcome {{ name }}",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        public TemplateEntityBuilder WithId(Guid id) { _template.Id = id; return this; }
        public TemplateEntityBuilder WithTenantId(Guid tenantId) { _template.TenantId = tenantId; return this; }
        public TemplateEntityBuilder WithName(string name) { _template.Name = name; return this; }

        public Template Build() => _template;
    }

    public sealed class SendingDomainEntityBuilder
    {
        private readonly SendingDomain _domain = new()
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            DomainName = "verified.com",
            Status = DomainStatus.Verified,
            CreatedAt = DateTime.UtcNow
        };

        public SendingDomainEntityBuilder WithId(Guid id) { _domain.Id = id; return this; }
        public SendingDomainEntityBuilder WithTenantId(Guid tenantId) { _domain.TenantId = tenantId; return this; }
        public SendingDomainEntityBuilder WithDomainName(string name) { _domain.DomainName = name; return this; }
        public SendingDomainEntityBuilder WithStatus(DomainStatus status) { _domain.Status = status; return this; }

        public SendingDomain Build() => _domain;
    }

    public sealed class CreateInboundRuleCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private string _name = "Catch-All Rule";
        private Guid _domainId = Guid.NewGuid();
        private string _matchPattern = "*@";
        private InboundRuleAction _action = InboundRuleAction.Store;
        private string? _webhookUrl;
        private string? _forwardTo;
        private int _priority;

        public CreateInboundRuleCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public CreateInboundRuleCommandBuilder WithName(string name) { _name = name; return this; }
        public CreateInboundRuleCommandBuilder WithDomainId(Guid domainId) { _domainId = domainId; return this; }
        public CreateInboundRuleCommandBuilder WithMatchPattern(string pattern) { _matchPattern = pattern; return this; }
        public CreateInboundRuleCommandBuilder WithAction(InboundRuleAction action) { _action = action; return this; }
        public CreateInboundRuleCommandBuilder WithWebhookUrl(string? url) { _webhookUrl = url; return this; }
        public CreateInboundRuleCommandBuilder WithForwardTo(string? email) { _forwardTo = email; return this; }
        public CreateInboundRuleCommandBuilder WithPriority(int priority) { _priority = priority; return this; }

        public CreateInboundRuleCommand Build() => new(
            _tenantId, _name, _domainId, _matchPattern, _action,
            _webhookUrl, _forwardTo, _priority);
    }

    public sealed class InboundRuleEntityBuilder
    {
        private readonly InboundRule _rule = new()
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            DomainId = Guid.NewGuid(),
            Name = "Catch-All Rule",
            MatchPattern = "*@",
            Action = InboundRuleAction.Store,
            IsActive = true,
            Priority = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        public InboundRuleEntityBuilder WithId(Guid id) { _rule.Id = id; return this; }
        public InboundRuleEntityBuilder WithTenantId(Guid tenantId) { _rule.TenantId = tenantId; return this; }
        public InboundRuleEntityBuilder WithDomainId(Guid domainId) { _rule.DomainId = domainId; return this; }
        public InboundRuleEntityBuilder WithName(string name) { _rule.Name = name; return this; }
        public InboundRuleEntityBuilder WithMatchPattern(string pattern) { _rule.MatchPattern = pattern; return this; }
        public InboundRuleEntityBuilder WithAction(InboundRuleAction action) { _rule.Action = action; return this; }
        public InboundRuleEntityBuilder WithWebhookUrl(string? url) { _rule.WebhookUrl = url; return this; }
        public InboundRuleEntityBuilder WithForwardTo(string? email) { _rule.ForwardTo = email; return this; }
        public InboundRuleEntityBuilder WithIsActive(bool isActive) { _rule.IsActive = isActive; return this; }
        public InboundRuleEntityBuilder WithPriority(int priority) { _rule.Priority = priority; return this; }

        public InboundRule Build() => _rule;
    }

    public sealed class InboundEmailEntityBuilder
    {
        private readonly InboundEmail _email = new()
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            MessageId = $"<{Guid.NewGuid():N}@example.com>",
            FromEmail = "sender@example.com",
            FromName = "Sender",
            ToEmails = "[\"recipient@verified.com\"]",
            CcEmails = "[]",
            BccEmails = "[]",
            Subject = "Test Inbound",
            HtmlBody = "<p>Hello</p>",
            TextBody = "Hello",
            Headers = "{}",
            Status = InboundEmailStatus.Received,
            Metadata = "{}",
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        public InboundEmailEntityBuilder WithId(Guid id) { _email.Id = id; return this; }
        public InboundEmailEntityBuilder WithTenantId(Guid tenantId) { _email.TenantId = tenantId; return this; }
        public InboundEmailEntityBuilder WithFromEmail(string from) { _email.FromEmail = from; return this; }
        public InboundEmailEntityBuilder WithSubject(string? subject) { _email.Subject = subject; return this; }
        public InboundEmailEntityBuilder WithStatus(InboundEmailStatus status) { _email.Status = status; return this; }
        public InboundEmailEntityBuilder WithInReplyTo(string? inReplyTo) { _email.InReplyTo = inReplyTo; return this; }
        public InboundEmailEntityBuilder WithOutboundEmailId(Guid? id) { _email.OutboundEmailId = id; return this; }

        public InboundEmail Build() => _email;
    }

    public sealed class AdminLoginCommandBuilder
    {
        private string _email = "admin@test.com";
        private string _password = "Test123!";
        private string _ipAddress = "127.0.0.1";

        public AdminLoginCommandBuilder WithEmail(string email) { _email = email; return this; }
        public AdminLoginCommandBuilder WithPassword(string password) { _password = password; return this; }
        public AdminLoginCommandBuilder WithIpAddress(string ipAddress) { _ipAddress = ipAddress; return this; }

        public AdminLoginCommand Build() => new(_email, _password, _ipAddress);
    }

    public sealed class CreateTenantCommandBuilder
    {
        private Guid _adminUserId = Guid.NewGuid();
        private string _name = "Test Tenant";
        private string? _contactEmail;
        private string? _companyName;
        private string _legalEntityName = "Test Legal Ltd.";
        private string _postalAddress = "123 Test St, Lagos, Nigeria";
        private int? _maxApiKeys;
        private int? _maxDomainsCount;
        private long? _monthlyEmailLimit;
        private string? _notes;

        public CreateTenantCommandBuilder WithAdminUserId(Guid id) { _adminUserId = id; return this; }
        public CreateTenantCommandBuilder WithName(string name) { _name = name; return this; }
        public CreateTenantCommandBuilder WithContactEmail(string? email) { _contactEmail = email; return this; }
        public CreateTenantCommandBuilder WithCompanyName(string? name) { _companyName = name; return this; }
        public CreateTenantCommandBuilder WithLegalEntityName(string name) { _legalEntityName = name; return this; }
        public CreateTenantCommandBuilder WithPostalAddress(string addr) { _postalAddress = addr; return this; }
        public CreateTenantCommandBuilder WithMaxApiKeys(int? max) { _maxApiKeys = max; return this; }
        public CreateTenantCommandBuilder WithMaxDomainsCount(int? max) { _maxDomainsCount = max; return this; }
        public CreateTenantCommandBuilder WithMonthlyEmailLimit(long? limit) { _monthlyEmailLimit = limit; return this; }
        public CreateTenantCommandBuilder WithNotes(string? notes) { _notes = notes; return this; }

        public CreateTenantCommand Build() => new(
            _adminUserId, _name, _contactEmail, _companyName,
            _legalEntityName, _postalAddress,
            _maxApiKeys, _maxDomainsCount, _monthlyEmailLimit, _notes);
    }

    public sealed class CreateAdminUserCommandBuilder
    {
        private Guid _adminUserId = Guid.NewGuid();
        private string _email = "newadmin@test.com";
        private string _displayName = "New Admin";
        private string _password = "Test123!Password";
        private string _role = "Admin";

        public CreateAdminUserCommandBuilder WithAdminUserId(Guid id) { _adminUserId = id; return this; }
        public CreateAdminUserCommandBuilder WithEmail(string email) { _email = email; return this; }
        public CreateAdminUserCommandBuilder WithDisplayName(string name) { _displayName = name; return this; }
        public CreateAdminUserCommandBuilder WithPassword(string password) { _password = password; return this; }
        public CreateAdminUserCommandBuilder WithRole(string role) { _role = role; return this; }

        public CreateAdminUserCommand Build() => new(
            _adminUserId, _email, _displayName, _password, _role);
    }

    public sealed class AdminUserEntityBuilder
    {
        private readonly AdminUser _user = new()
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            DisplayName = "Test Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = AdminRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        public AdminUserEntityBuilder WithId(Guid id) { _user.Id = id; return this; }
        public AdminUserEntityBuilder WithEmail(string email) { _user.Email = email; return this; }
        public AdminUserEntityBuilder WithDisplayName(string name) { _user.DisplayName = name; return this; }
        public AdminUserEntityBuilder WithPasswordHash(string hash) { _user.PasswordHash = hash; return this; }
        public AdminUserEntityBuilder WithRole(AdminRole role) { _user.Role = role; return this; }
        public AdminUserEntityBuilder WithIsActive(bool isActive) { _user.IsActive = isActive; return this; }
        public AdminUserEntityBuilder WithLastLoginAt(DateTime? lastLogin) { _user.LastLoginAt = lastLogin; return this; }

        public AdminUser Build() => _user;
    }

    public sealed class TenantEntityBuilder
    {
        private readonly Tenant _tenant = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Tenant",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        public TenantEntityBuilder WithId(Guid id) { _tenant.Id = id; return this; }
        public TenantEntityBuilder WithName(string name) { _tenant.Name = name; return this; }
        public TenantEntityBuilder WithStatus(TenantStatus status) { _tenant.Status = status; return this; }
        public TenantEntityBuilder WithCompanyName(string? name) { _tenant.CompanyName = name; return this; }
        public TenantEntityBuilder WithContactEmail(string? email) { _tenant.ContactEmail = email; return this; }
        public TenantEntityBuilder WithPasswordHash(string? hash) { _tenant.PasswordHash = hash; return this; }
        public TenantEntityBuilder WithNotes(string? notes) { _tenant.Notes = notes; return this; }
        public TenantEntityBuilder WithMonthlyEmailLimit(long? limit) { _tenant.MonthlyEmailLimit = limit; return this; }
        public TenantEntityBuilder WithMaxApiKeys(int? max) { _tenant.MaxApiKeys = max; return this; }
        public TenantEntityBuilder WithMaxDomainsCount(int? max) { _tenant.MaxDomainsCount = max; return this; }

        public Tenant Build() => _tenant;
    }

    public sealed class PlanEntityBuilder
    {
        private readonly Plan _plan = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            Tier = PlanTier.Free,
            MonthlyPriceUsd = 0m,
            AnnualPriceUsd = 0m,
            DailyEmailLimit = 100,
            MonthlyEmailLimit = 3000,
            MaxApiKeys = 3,
            MaxDomains = 2,
            MaxTemplates = 10,
            MaxWebhooks = 5,
            CustomDomainBranding = false,
            PrioritySupport = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        public PlanEntityBuilder WithId(Guid id) { _plan.Id = id; return this; }
        public PlanEntityBuilder WithName(string name) { _plan.Name = name; return this; }
        public PlanEntityBuilder WithTier(PlanTier tier) { _plan.Tier = tier; return this; }
        public PlanEntityBuilder WithMonthlyPriceUsd(decimal price) { _plan.MonthlyPriceUsd = price; return this; }
        public PlanEntityBuilder WithAnnualPriceUsd(decimal price) { _plan.AnnualPriceUsd = price; return this; }
        public PlanEntityBuilder WithDailyEmailLimit(int limit) { _plan.DailyEmailLimit = limit; return this; }
        public PlanEntityBuilder WithMonthlyEmailLimit(long limit) { _plan.MonthlyEmailLimit = limit; return this; }
        public PlanEntityBuilder WithMaxApiKeys(int max) { _plan.MaxApiKeys = max; return this; }
        public PlanEntityBuilder WithMaxDomains(int max) { _plan.MaxDomains = max; return this; }
        public PlanEntityBuilder WithMaxTemplates(int max) { _plan.MaxTemplates = max; return this; }
        public PlanEntityBuilder WithMaxWebhooks(int max) { _plan.MaxWebhooks = max; return this; }
        public PlanEntityBuilder WithCustomDomainBranding(bool enabled) { _plan.CustomDomainBranding = enabled; return this; }
        public PlanEntityBuilder WithPrioritySupport(bool enabled) { _plan.PrioritySupport = enabled; return this; }
        public PlanEntityBuilder WithIsActive(bool isActive) { _plan.IsActive = isActive; return this; }

        public Plan Build() => _plan;
    }

    public sealed class CreatePlanCommandBuilder
    {
        private string _name = "Test Plan";
        private PlanTier _tier = PlanTier.Free;
        private decimal _monthlyPriceUsd;
        private decimal _annualPriceUsd;
        private int _dailyEmailLimit = 100;
        private long _monthlyEmailLimit = 3000;
        private int _maxApiKeys = 3;
        private int _maxDomains = 2;
        private int _maxTemplates = 10;
        private int _maxWebhooks = 5;
        private bool _customDomainBranding;
        private bool _prioritySupport;

        public CreatePlanCommandBuilder WithName(string name) { _name = name; return this; }
        public CreatePlanCommandBuilder WithTier(PlanTier tier) { _tier = tier; return this; }
        public CreatePlanCommandBuilder WithMonthlyPriceUsd(decimal price) { _monthlyPriceUsd = price; return this; }
        public CreatePlanCommandBuilder WithAnnualPriceUsd(decimal price) { _annualPriceUsd = price; return this; }
        public CreatePlanCommandBuilder WithDailyEmailLimit(int limit) { _dailyEmailLimit = limit; return this; }
        public CreatePlanCommandBuilder WithMonthlyEmailLimit(long limit) { _monthlyEmailLimit = limit; return this; }
        public CreatePlanCommandBuilder WithMaxApiKeys(int max) { _maxApiKeys = max; return this; }
        public CreatePlanCommandBuilder WithMaxDomains(int max) { _maxDomains = max; return this; }
        public CreatePlanCommandBuilder WithMaxTemplates(int max) { _maxTemplates = max; return this; }
        public CreatePlanCommandBuilder WithMaxWebhooks(int max) { _maxWebhooks = max; return this; }
        public CreatePlanCommandBuilder WithCustomDomainBranding(bool enabled) { _customDomainBranding = enabled; return this; }
        public CreatePlanCommandBuilder WithPrioritySupport(bool enabled) { _prioritySupport = enabled; return this; }

        public CreatePlanCommand Build() => new(
            _name, _tier, _monthlyPriceUsd, _annualPriceUsd,
            _dailyEmailLimit, _monthlyEmailLimit,
            _maxApiKeys, _maxDomains, _maxTemplates, _maxWebhooks,
            _customDomainBranding, _prioritySupport);
    }

    public sealed class SendBatchCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private Guid _apiKeyId = DefaultApiKeyId;
        private List<BatchEmailItem> _emails = new()
        {
            new BatchEmailItem(
                "sender@verified.com",
                new List<string> { "recipient@example.com" },
                null, null, "Test Subject", "<p>Hello</p>", "Hello",
                null, null, null, null)
        };

        public SendBatchCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public SendBatchCommandBuilder WithApiKeyId(Guid apiKeyId) { _apiKeyId = apiKeyId; return this; }
        public SendBatchCommandBuilder WithEmails(List<BatchEmailItem> emails) { _emails = emails; return this; }
        public SendBatchCommandBuilder WithNullEmails() { _emails = null!; return this; }

        public SendBatchCommand Build() => new(_tenantId, _apiKeyId, _emails);
    }

    public sealed class ScheduleEmailCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private Guid _apiKeyId = DefaultApiKeyId;
        private string _from = "sender@example.com";
        private string _to = "recipient@example.com";
        private string _subject = "Test Subject";
        private string? _htmlBody = "<p>Hello</p>";
        private string? _textBody;
        private Guid? _templateId;
        private Dictionary<string, string>? _variables;
        private DateTime _scheduledAt = DateTime.UtcNow.AddHours(1);

        public ScheduleEmailCommandBuilder WithFrom(string from) { _from = from; return this; }
        public ScheduleEmailCommandBuilder WithTo(string to) { _to = to; return this; }
        public ScheduleEmailCommandBuilder WithSubject(string subject) { _subject = subject; return this; }
        public ScheduleEmailCommandBuilder WithHtmlBody(string? htmlBody) { _htmlBody = htmlBody; return this; }
        public ScheduleEmailCommandBuilder WithTextBody(string? textBody) { _textBody = textBody; return this; }
        public ScheduleEmailCommandBuilder WithTemplateId(Guid? templateId) { _templateId = templateId; return this; }
        public ScheduleEmailCommandBuilder WithVariables(Dictionary<string, string>? variables) { _variables = variables; return this; }
        public ScheduleEmailCommandBuilder WithScheduledAt(DateTime scheduledAt) { _scheduledAt = scheduledAt; return this; }

        public ScheduleEmailCommand Build() => new(
            _tenantId, _apiKeyId, _from, _to, _subject,
            _htmlBody, _textBody, _templateId, _variables, _scheduledAt);
    }

    public sealed class CreateSubscriptionCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private Guid _planId = Guid.NewGuid();
        private string? _provider;

        public CreateSubscriptionCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public CreateSubscriptionCommandBuilder WithPlanId(Guid planId) { _planId = planId; return this; }
        public CreateSubscriptionCommandBuilder WithProvider(string? provider) { _provider = provider; return this; }

        public CreateSubscriptionCommand Build() => new(_tenantId, _planId, _provider);
    }

    public sealed class AddSuppressionCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private string _emailAddress = "suppressed@example.com";

        public AddSuppressionCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public AddSuppressionCommandBuilder WithEmailAddress(string emailAddress) { _emailAddress = emailAddress; return this; }

        public AddSuppressionCommand Build() => new(_tenantId, _emailAddress);
    }

    public sealed class CreateWebhookCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private string _url = "https://example.com/webhook";
        private string[] _events = { "sent", "delivered" };
        private string? _secret;

        public CreateWebhookCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public CreateWebhookCommandBuilder WithUrl(string url) { _url = url; return this; }
        public CreateWebhookCommandBuilder WithEvents(string[] events) { _events = events; return this; }
        public CreateWebhookCommandBuilder WithSecret(string? secret) { _secret = secret; return this; }

        public CreateWebhookCommand Build() => new(_tenantId, _url, _events, _secret);
    }

    public sealed class UpdateWebhookCommandBuilder
    {
        private Guid _id = Guid.NewGuid();
        private Guid _tenantId = DefaultTenantId;
        private string? _url;
        private string[]? _events;
        private string? _status;

        public UpdateWebhookCommandBuilder WithId(Guid id) { _id = id; return this; }
        public UpdateWebhookCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public UpdateWebhookCommandBuilder WithUrl(string? url) { _url = url; return this; }
        public UpdateWebhookCommandBuilder WithEvents(string[]? events) { _events = events; return this; }
        public UpdateWebhookCommandBuilder WithStatus(string? status) { _status = status; return this; }

        public UpdateWebhookCommand Build() => new(_id, _tenantId, _url, _events, _status);
    }

    public sealed class UpdateInboundRuleCommandBuilder
    {
        private Guid _tenantId = DefaultTenantId;
        private Guid _ruleId = Guid.NewGuid();
        private string? _name;
        private string? _matchPattern;
        private InboundRuleAction? _action;
        private string? _webhookUrl;
        private string? _forwardTo;
        private bool? _isActive;
        private int? _priority;

        public UpdateInboundRuleCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public UpdateInboundRuleCommandBuilder WithRuleId(Guid ruleId) { _ruleId = ruleId; return this; }
        public UpdateInboundRuleCommandBuilder WithName(string? name) { _name = name; return this; }
        public UpdateInboundRuleCommandBuilder WithMatchPattern(string? matchPattern) { _matchPattern = matchPattern; return this; }
        public UpdateInboundRuleCommandBuilder WithAction(InboundRuleAction? action) { _action = action; return this; }
        public UpdateInboundRuleCommandBuilder WithWebhookUrl(string? webhookUrl) { _webhookUrl = webhookUrl; return this; }
        public UpdateInboundRuleCommandBuilder WithForwardTo(string? forwardTo) { _forwardTo = forwardTo; return this; }
        public UpdateInboundRuleCommandBuilder WithIsActive(bool? isActive) { _isActive = isActive; return this; }
        public UpdateInboundRuleCommandBuilder WithPriority(int? priority) { _priority = priority; return this; }

        public UpdateInboundRuleCommand Build() => new(
            _tenantId, _ruleId, _name, _matchPattern, _action,
            _webhookUrl, _forwardTo, _isActive, _priority);
    }
}
