-- ============================================================================
-- migrate_mailgun_webhooks_dedup.sql
--
-- Idempotent stopgap that applies two EF Core migrations which were never
-- executed against production Postgres because the deploy pipeline only runs
-- a hardcoded list of legacy SQL files and never invokes `dotnet ef database
-- update`. Production signup was returning 500 because the tenants/emails
-- columns below did not exist.
--
-- Migrations reproduced here (match Up() from the EF-generated C# migrations):
--   1) 20260414154548_AddEmailProviderColumns
--      - tenants.preferred_email_provider_key   varchar(32) NULL
--      - emails.provider_key                    varchar(32) NULL
--      - emails.provider_message_id             varchar(255) NULL
--   2) 20260414185323_AddWebhookDeliveriesDedup
--      - creates webhook_deliveries table + unique dedup index + FK to webhooks
--
-- Every statement uses IF NOT EXISTS guards so the script is safe to re-run.
-- After applying schema, both MigrationIds are inserted into
-- __EFMigrationsHistory so a future `dotnet ef database update` will not try
-- to re-apply them.
--
-- THIS IS A STOPGAP. The proper fix is to run `dotnet ef database update`
-- as part of the deploy pipeline (see deploy.sh comment near the migration
-- block). Do not add new migrations here — add them to the EF migrations
-- folder and let the tool apply them.
-- ============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- Migration 1: 20260414154548_AddEmailProviderColumns
-- ---------------------------------------------------------------------------

ALTER TABLE tenants
    ADD COLUMN IF NOT EXISTS preferred_email_provider_key character varying(32) NULL;

ALTER TABLE emails
    ADD COLUMN IF NOT EXISTS provider_key character varying(32) NULL;

ALTER TABLE emails
    ADD COLUMN IF NOT EXISTS provider_message_id character varying(255) NULL;

-- EF migration also issues an UpdateData on the seeded default tenant to set
-- preferred_email_provider_key = NULL. The column already defaults to NULL on
-- ADD COLUMN, so this is a no-op in practice — included for fidelity.
UPDATE tenants
SET preferred_email_provider_key = NULL
WHERE id = '00000000-0000-0000-0000-000000000001'
  AND preferred_email_provider_key IS DISTINCT FROM NULL;

-- ---------------------------------------------------------------------------
-- Migration 2: 20260414185323_AddWebhookDeliveriesDedup
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS webhook_deliveries (
    id                     uuid                     NOT NULL DEFAULT gen_random_uuid(),
    webhook_id             uuid                     NOT NULL,
    email_id               uuid                     NOT NULL,
    event_type             character varying(50)    NOT NULL,
    status                 character varying(16)    NOT NULL,
    first_attempt_at       timestamp with time zone NOT NULL DEFAULT NOW(),
    last_attempt_at        timestamp with time zone NOT NULL,
    attempt_count          integer                  NOT NULL DEFAULT 0,
    response_status_code   integer                  NULL,
    response_body_snippet  character varying(1024)  NULL,
    CONSTRAINT "PK_webhook_deliveries" PRIMARY KEY (id),
    CONSTRAINT "FK_webhook_deliveries_webhooks_webhook_id"
        FOREIGN KEY (webhook_id) REFERENCES webhooks (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_webhook_deliveries_dedup
    ON webhook_deliveries (webhook_id, email_id, event_type);

-- ---------------------------------------------------------------------------
-- Record both migrations in __EFMigrationsHistory so EF treats them as
-- already applied. ProductVersion matches the value stamped in the Designer
-- files (10.0.0).
-- ---------------------------------------------------------------------------

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260414154548_AddEmailProviderColumns', '10.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260414185323_AddWebhookDeliveriesDedup', '10.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
