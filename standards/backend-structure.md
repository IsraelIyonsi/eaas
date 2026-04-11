# Backend Structure & Entity Patterns

## 1.1 Project Structure

Clean Architecture with vertical slice features:

```
src/
  EaaS.Domain/          # Entities, Enums, Exceptions, Interfaces (zero dependencies)
  EaaS.Shared/          # Constants, Models (ApiResponse, PagedResponse), Utilities
  EaaS.Infrastructure/  # EF Core, Redis, MassTransit, AWS services
  EaaS.Api/             # Minimal API endpoints, MediatR handlers, validators
  EaaS.Worker/          # Background consumer host
  EaaS.WebhookProcessor/ # SES webhook ingestion
```

**Where things go:**

| Artifact | Location |
|---|---|
| Entities | `EaaS.Domain/Entities/` |
| Enums | `EaaS.Domain/Enums/` |
| Interfaces | `EaaS.Domain/Interfaces/` |
| Domain exceptions | `EaaS.Domain/Exceptions/` |
| EF configurations | `EaaS.Infrastructure/Persistence/Configurations/` |
| Services (S3, SES, Redis) | `EaaS.Infrastructure/Services/` |
| MassTransit consumers | `EaaS.Infrastructure/Messaging/` |
| Message contracts | `EaaS.Infrastructure/Messaging/Contracts/` |
| Shared constants | `EaaS.Shared/Constants/` |
| API response models | `EaaS.Shared/Models/` (namespace: `EaaS.Shared.Contracts`) |
| Route/tag constants | `EaaS.Api/Constants/` |
| Auth handlers | `EaaS.Api/Authentication/` |

## 1.2 Entity Pattern

Plain POCOs, no base class:

```csharp
public class InboundEmail
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string MessageId { get; set; } = string.Empty;       // Required: string.Empty
    public string? FromName { get; set; }                        // Optional: nullable
    public string ToEmails { get; set; } = "[]";                 // JSON: "[]" or "{}"
    public string[] Tags { get; set; } = Array.Empty<string>();  // Array: Array.Empty
    public InboundEmailStatus Status { get; set; } = InboundEmailStatus.Received;
    public DateTime CreatedAt { get; set; }
    public Tenant Tenant { get; set; } = null!;                  // Required nav: null!
    public Email? OutboundEmail { get; set; }                    // Optional nav: ?
    public ICollection<InboundAttachment> Attachments { get; set; } = new List<InboundAttachment>();
}
```

## 1.3 EF Core Configuration

Every entity gets a dedicated `IEntityTypeConfiguration<T>`:
- Table name: snake_case via `.ToTable()`
- All columns: snake_case via `.HasColumnName()`
- Primary key: `.HasDefaultValueSql("gen_random_uuid()")`
- JSON columns: `.HasColumnType("jsonb")`, defaults `'[]'::jsonb`
- Array columns: `.HasColumnType("text[]")`
- Index naming: `idx_tablename_column`

**Enum Registration (DUAL requirement):**
1. `AppDbContext.OnModelCreating`: `modelBuilder.HasPostgresEnum<MyEnum>();`
2. `DependencyInjection.cs`: `dataSourceBuilder.MapEnum<MyEnum>();`

Missing either causes runtime failures.
