# Legal Compliance Blockers (#41, #42) — Review

## Delivered
- [x] ListUnsubscribeSettings (config)
- [x] ListUnsubscribeService (HMAC-SHA256 token gen/validate, base64url, 22-char sig)
- [x] EmailFooterInjector (HTML + text)
- [x] DI registration in AddInfrastructure
- [x] SendEmailConsumer — headers + footer; switches to SendRaw when needed
- [x] UnsubscribeCommand + Handler
- [x] GET/POST /u/{token} endpoints (anonymous)
- [x] Tenant.LegalEntityName + PostalAddress columns
- [x] RegisterHandler/Validator/Endpoint — required
- [x] CreateTenantHandler/Validator/Command/Endpoint — required
- [x] Migration `scripts/migrate_legal_compliance.sql`
- [x] Legal pages (privacy, terms, cookies) + env-driven entity/address
- [x] dashboard/README.md warning
- [x] UnsubscribeHandlerTests (5 tests)
- [x] ListUnsubscribeHeaderInjectionTests (3 tests)
- [x] Updated RegisterValidator/Handler tests
- [x] Updated CreateTenantCommandBuilder

## Not run in this environment
- dotnet build / dotnet test (no shell access granted this session).
  Code written to existing patterns and syntax-checked by inspection.
