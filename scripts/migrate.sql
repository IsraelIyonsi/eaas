-- =============================================================================
-- EaaS Database Migration Script
-- Creates all enum types, tables, indexes, and seed data
-- Matches EF Core entity configurations exactly
-- =============================================================================

BEGIN;

-- ============================================================
-- 1. CUSTOM ENUM TYPES
-- ============================================================
DO $$ BEGIN
    CREATE TYPE email_status AS ENUM (
        'queued', 'sending', 'sent', 'delivered', 'bounced', 'complained', 'failed'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE event_type AS ENUM (
        'queued', 'sent', 'delivered', 'bounced', 'complained', 'opened', 'clicked', 'failed'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE domain_status AS ENUM (
        'pending_verification', 'verified', 'failed', 'suspended'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE api_key_status AS ENUM (
        'active', 'revoked', 'rotating'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE suppression_reason AS ENUM (
        'hard_bounce', 'soft_bounce_limit', 'complaint', 'manual'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE dns_record_purpose AS ENUM (
        'spf', 'dkim', 'dmarc'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ============================================================
-- 2. TABLES
-- ============================================================

-- 2.1 tenants
CREATE TABLE IF NOT EXISTS tenants (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(255) NOT NULL,
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- 2.2 api_keys
CREATE TABLE IF NOT EXISTS api_keys (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    name            VARCHAR(255) NOT NULL,
    key_hash        VARCHAR(64) NOT NULL,
    prefix          VARCHAR(16) NOT NULL,
    allowed_domains TEXT[] DEFAULT '{}',
    status          api_key_status NOT NULL DEFAULT 'active',
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    last_used_at    TIMESTAMP WITH TIME ZONE,
    revoked_at      TIMESTAMP WITH TIME ZONE,
    CONSTRAINT uq_api_keys_key_hash UNIQUE (key_hash)
);

CREATE INDEX IF NOT EXISTS idx_api_keys_tenant ON api_keys(tenant_id);
CREATE INDEX IF NOT EXISTS idx_api_keys_status ON api_keys(tenant_id, status) WHERE status = 'active';

-- 2.3 domains
CREATE TABLE IF NOT EXISTS domains (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    domain_name     VARCHAR(255) NOT NULL,
    status          domain_status NOT NULL DEFAULT 'pending_verification',
    ses_identity_arn VARCHAR(512),
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    verified_at     TIMESTAMP WITH TIME ZONE,
    last_checked_at TIMESTAMP WITH TIME ZONE,
    CONSTRAINT uq_domains_tenant_name UNIQUE (tenant_id, domain_name)
);

CREATE INDEX IF NOT EXISTS idx_domains_tenant ON domains(tenant_id);
CREATE INDEX IF NOT EXISTS idx_domains_status ON domains(tenant_id, status);

-- 2.4 dns_records
CREATE TABLE IF NOT EXISTS dns_records (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    domain_id       UUID NOT NULL REFERENCES domains(id) ON DELETE CASCADE,
    record_type     VARCHAR(10) NOT NULL,
    record_name     VARCHAR(512) NOT NULL,
    record_value    VARCHAR(1024) NOT NULL,
    purpose         dns_record_purpose NOT NULL,
    is_verified     BOOLEAN NOT NULL DEFAULT FALSE,
    verified_at     TIMESTAMP WITH TIME ZONE,
    actual_value    VARCHAR(1024),
    updated_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_dns_records_domain ON dns_records(domain_id);

-- 2.5 templates
CREATE TABLE IF NOT EXISTS templates (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    name            VARCHAR(255) NOT NULL,
    subject_template VARCHAR(1024) NOT NULL,
    html_body       TEXT NOT NULL,
    text_body       TEXT,
    variables_schema JSONB,
    version         INTEGER NOT NULL DEFAULT 1,
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    deleted_at      TIMESTAMP WITH TIME ZONE
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_templates_tenant_name_active
    ON templates(tenant_id, name)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_templates_tenant ON templates(tenant_id);
CREATE INDEX IF NOT EXISTS idx_templates_deleted ON templates(tenant_id, deleted_at) WHERE deleted_at IS NULL;

-- 2.6 emails
CREATE TABLE IF NOT EXISTS emails (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    api_key_id      UUID NOT NULL REFERENCES api_keys(id) ON DELETE RESTRICT,
    message_id      VARCHAR(32) NOT NULL,
    batch_id        VARCHAR(32),
    from_email      VARCHAR(320) NOT NULL,
    from_name       VARCHAR(255),
    to_emails       JSONB NOT NULL,
    cc_emails       JSONB DEFAULT '[]'::jsonb,
    bcc_emails      JSONB DEFAULT '[]'::jsonb,
    subject         VARCHAR(998) NOT NULL,
    html_body       TEXT,
    text_body       TEXT,
    template_id     UUID,
    variables       JSONB,
    attachments     JSONB DEFAULT '[]'::jsonb,
    tags            TEXT[] DEFAULT '{}',
    metadata        JSONB DEFAULT '{}'::jsonb,
    track_opens     BOOLEAN NOT NULL DEFAULT TRUE,
    track_clicks    BOOLEAN NOT NULL DEFAULT TRUE,
    status          email_status NOT NULL DEFAULT 'queued',
    ses_message_id  VARCHAR(255),
    error_message   TEXT,
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    sent_at         TIMESTAMP WITH TIME ZONE,
    delivered_at    TIMESTAMP WITH TIME ZONE,
    opened_at       TIMESTAMP WITH TIME ZONE,
    clicked_at      TIMESTAMP WITH TIME ZONE
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_emails_message_id ON emails(message_id);
CREATE INDEX IF NOT EXISTS idx_emails_tenant_status ON emails(tenant_id, status, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_emails_tenant_created ON emails(tenant_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_emails_batch ON emails(batch_id) WHERE batch_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_emails_template ON emails(template_id) WHERE template_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_emails_api_key ON emails(api_key_id);
CREATE INDEX IF NOT EXISTS idx_emails_from ON emails(from_email);

-- 2.7 email_events
CREATE TABLE IF NOT EXISTS email_events (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email_id        UUID NOT NULL REFERENCES emails(id) ON DELETE CASCADE,
    event_type      event_type NOT NULL,
    data            JSONB DEFAULT '{}'::jsonb,
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_email_events_email ON email_events(email_id);
CREATE INDEX IF NOT EXISTS idx_email_events_type ON email_events(event_type, created_at);

-- 2.8 suppression_list
CREATE TABLE IF NOT EXISTS suppression_list (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    email_address   VARCHAR(320) NOT NULL,
    reason          suppression_reason NOT NULL,
    source_message_id VARCHAR(32),
    suppressed_at   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_suppression_tenant_email UNIQUE (tenant_id, email_address)
);

CREATE INDEX IF NOT EXISTS idx_suppression_tenant ON suppression_list(tenant_id);
CREATE INDEX IF NOT EXISTS idx_suppression_email ON suppression_list(email_address);

-- 2.9 webhooks
CREATE TABLE IF NOT EXISTS webhooks (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE RESTRICT,
    url             VARCHAR(2048) NOT NULL,
    events          TEXT[] NOT NULL,
    secret          VARCHAR(255),
    status          VARCHAR(20) NOT NULL DEFAULT 'active',
    consecutive_failures INT NOT NULL DEFAULT 0,
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

ALTER TABLE webhooks ADD COLUMN IF NOT EXISTS consecutive_failures INT NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS idx_webhooks_tenant ON webhooks(tenant_id);

-- ============================================================
-- 3. EF CORE MIGRATIONS HISTORY TABLE
-- ============================================================
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId"       VARCHAR(150) NOT NULL,
    "ProductVersion"    VARCHAR(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- ============================================================
-- 4. SEED DATA
-- ============================================================

-- Default tenant
INSERT INTO tenants (id, name, created_at, updated_at)
VALUES ('00000000-0000-0000-0000-000000000001', 'Default', '2026-03-27T00:00:00Z', '2026-03-27T00:00:00Z')
ON CONFLICT (id) DO NOTHING;

COMMIT;

BEGIN;
ALTER TABLE emails ALTER COLUMN message_id TYPE VARCHAR(64);
COMMIT;
