-- =============================================================================
-- Schema Drift Remediation Migration
-- Generated: 2026-04-14 05:12 UTC
-- Author: Israel Iyonsi
-- Purpose: Sync live `eaas` database with EF Core model definitions after
--          discovering drift caused by a manual schema bootstrap
--          (__EFMigrationsHistory was empty in prod).
--
-- Root cause of prod 500 on POST /api/v1/webhooks:
--   EF model expects `webhooks.consecutive_failures` column, prod was missing it.
--
-- Additional drift discovered during audit:
--   * tenants.legal_entity_name (CAN-SPAM compliance, used by RegisterHandler)
--   * tenants.postal_address   (CAN-SPAM compliance, used by RegisterHandler)
--   * emails: missing partial index ix_emails_scheduled (scheduled-email scan)
--
-- Safety guarantees:
--   - All statements are idempotent (IF NOT EXISTS).
--   - No DROP TABLE, no DESTRUCTIVE operations.
--   - All new columns are NULLABLE or have server DEFAULTs -> existing rows
--     backfill cleanly without violating constraints.
--   - Wrapped in a single transaction so partial apply is impossible.
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. webhooks.consecutive_failures  (CRITICAL — resolves prod 500)
-- -----------------------------------------------------------------------------
ALTER TABLE public.webhooks
    ADD COLUMN IF NOT EXISTS consecutive_failures integer NOT NULL DEFAULT 0;

-- -----------------------------------------------------------------------------
-- 2. tenants.legal_entity_name  (CAN-SPAM §7704(a)(5))
-- -----------------------------------------------------------------------------
ALTER TABLE public.tenants
    ADD COLUMN IF NOT EXISTS legal_entity_name varchar(255) NULL;

-- -----------------------------------------------------------------------------
-- 3. tenants.postal_address  (CAN-SPAM §7704(a)(5))
-- -----------------------------------------------------------------------------
ALTER TABLE public.tenants
    ADD COLUMN IF NOT EXISTS postal_address text NULL;

-- -----------------------------------------------------------------------------
-- 4. emails.ix_emails_scheduled  (scheduled-email dispatcher hot-path index)
-- -----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_emails_scheduled
    ON public.emails (status, scheduled_at)
    WHERE status = 'scheduled';

COMMIT;

-- =============================================================================
-- VERIFICATION (run manually after COMMIT):
--   \d webhooks
--   \d tenants
--   \d emails
--   SELECT column_name FROM information_schema.columns
--     WHERE table_name='webhooks' AND column_name='consecutive_failures';
-- =============================================================================

-- =============================================================================
-- ROLLBACK (commented — run only if migration causes regression):
-- -----------------------------------------------------------------------------
-- BEGIN;
-- ALTER TABLE public.webhooks DROP COLUMN IF EXISTS consecutive_failures;
-- ALTER TABLE public.tenants  DROP COLUMN IF EXISTS legal_entity_name;
-- ALTER TABLE public.tenants  DROP COLUMN IF EXISTS postal_address;
-- DROP INDEX IF EXISTS public.ix_emails_scheduled;
-- COMMIT;
-- =============================================================================

-- =============================================================================
-- NON-BREAKING DRIFT (flagged, intentionally NOT fixed by this migration):
--
-- * timestamp without time zone vs timestamp with time zone
--     admin_users, audit_logs, inbound_emails, inbound_attachments,
--     inbound_rules.  Npgsql serializes DateTime correctly against both;
--     altering would rewrite full tables and risk downtime. Address in a
--     planned maintenance window, not in a hotfix.
--
-- * Index naming (ix_* vs uq_* / idx_*)
--     invoices.ix_invoices_number, plans.ix_plans_name,
--     subscriptions.ix_subscriptions_*.  EF expects different names but
--     the indexes functionally exist. Rename is cosmetic; deferred.
--
-- * emails.message_id is varchar(64) in DB, EF HasMaxLength(32)
--     EF is stricter; no runtime impact. Deferred.
--
-- * emails.tracking_id has a unique partial index in DB that EF config
--     does not declare as unique. Harmless; EF will not try to violate it.
-- =============================================================================
