-- Sprint 4: Inbound Mail Foundation
-- Migration script for PostgreSQL
-- Creates: 2 enum types, 3 tables, indexes

BEGIN;

-- Enum types (lowercase values to match Npgsql convention)
DO $$ BEGIN
    CREATE TYPE inbound_email_status AS ENUM ('received', 'processing', 'processed', 'forwarded', 'failed');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE inbound_rule_action AS ENUM ('webhook', 'forward', 'store');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

-- Table: inbound_emails
CREATE TABLE IF NOT EXISTS inbound_emails (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id),
    message_id          VARCHAR(255) NOT NULL,
    from_email          VARCHAR(255) NOT NULL,
    from_name           VARCHAR(255),
    to_emails           JSONB NOT NULL,
    cc_emails           JSONB DEFAULT '[]'::jsonb,
    bcc_emails          JSONB DEFAULT '[]'::jsonb,
    reply_to            VARCHAR(255),
    subject             VARCHAR(1024),
    html_body           TEXT,
    text_body           TEXT,
    headers             JSONB,
    tags                TEXT[] DEFAULT '{}',
    metadata            JSONB DEFAULT '{}'::jsonb,
    status              inbound_email_status NOT NULL DEFAULT 'received',
    s3_key              VARCHAR(512),
    spam_score          DECIMAL(5,2),
    spam_verdict        VARCHAR(20),
    virus_verdict       VARCHAR(20),
    spf_verdict         VARCHAR(20),
    dkim_verdict        VARCHAR(20),
    dmarc_verdict       VARCHAR(20),
    in_reply_to         VARCHAR(255),
    "references"        TEXT,
    outbound_email_id   UUID REFERENCES emails(id) ON DELETE SET NULL,
    received_at         TIMESTAMP NOT NULL DEFAULT NOW(),
    processed_at        TIMESTAMP,
    created_at          TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_inbound_emails_tenant ON inbound_emails(tenant_id);
CREATE INDEX IF NOT EXISTS idx_inbound_emails_tenant_received ON inbound_emails(tenant_id, received_at DESC);
CREATE INDEX IF NOT EXISTS idx_inbound_emails_from ON inbound_emails(tenant_id, from_email);
CREATE INDEX IF NOT EXISTS idx_inbound_emails_message_id ON inbound_emails(message_id);
CREATE INDEX IF NOT EXISTS idx_inbound_emails_in_reply_to ON inbound_emails(in_reply_to) WHERE in_reply_to IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_inbound_emails_outbound ON inbound_emails(outbound_email_id) WHERE outbound_email_id IS NOT NULL;

-- Table: inbound_attachments
CREATE TABLE IF NOT EXISTS inbound_attachments (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    inbound_email_id    UUID NOT NULL REFERENCES inbound_emails(id) ON DELETE CASCADE,
    filename            VARCHAR(255) NOT NULL,
    content_type        VARCHAR(100) NOT NULL,
    size_bytes          BIGINT NOT NULL,
    s3_key              VARCHAR(512) NOT NULL,
    content_id          VARCHAR(255),
    is_inline           BOOLEAN NOT NULL DEFAULT FALSE,
    created_at          TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_inbound_attachments_email ON inbound_attachments(inbound_email_id);

-- Table: inbound_rules
CREATE TABLE IF NOT EXISTS inbound_rules (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL REFERENCES tenants(id),
    domain_id           UUID NOT NULL REFERENCES domains(id) ON DELETE CASCADE,
    name                VARCHAR(100) NOT NULL,
    match_pattern       VARCHAR(255) NOT NULL,
    action              inbound_rule_action NOT NULL DEFAULT 'store',
    webhook_url         VARCHAR(2048),
    forward_to          VARCHAR(255),
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    priority            INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_inbound_rules_tenant ON inbound_rules(tenant_id);
CREATE INDEX IF NOT EXISTS idx_inbound_rules_domain ON inbound_rules(domain_id);

COMMIT;
