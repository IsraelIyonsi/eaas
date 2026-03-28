using EaaS.Api.Features.ApiKeys;
using EaaS.Api.Features.Domains;
using EaaS.Api.Features.Emails;
using EaaS.Api.Features.Templates;
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
        private string _htmlBody = "<h1>Welcome {{ name }}</h1>";
        private string? _textBody = "Welcome {{ name }}";

        public CreateTemplateCommandBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
        public CreateTemplateCommandBuilder WithName(string name) { _name = name; return this; }
        public CreateTemplateCommandBuilder WithSubjectTemplate(string subject) { _subjectTemplate = subject; return this; }
        public CreateTemplateCommandBuilder WithHtmlBody(string htmlBody) { _htmlBody = htmlBody; return this; }
        public CreateTemplateCommandBuilder WithTextBody(string? textBody) { _textBody = textBody; return this; }

        public CreateTemplateCommand Build() => new(_tenantId, _name, _subjectTemplate, _htmlBody, _textBody);
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

        public SendingDomainEntityBuilder WithTenantId(Guid tenantId) { _domain.TenantId = tenantId; return this; }
        public SendingDomainEntityBuilder WithDomainName(string name) { _domain.DomainName = name; return this; }
        public SendingDomainEntityBuilder WithStatus(DomainStatus status) { _domain.Status = status; return this; }

        public SendingDomain Build() => _domain;
    }
}
