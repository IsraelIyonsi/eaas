-- ============================================================
-- EaaS Sprint 2 Migration
-- ============================================================

BEGIN;

-- -----------------------------------------------------------
-- 1. Add tracking_id to emails (for open/click token lookup)
-- -----------------------------------------------------------
ALTER TABLE emails
    ADD COLUMN IF NOT EXISTS tracking_id VARCHAR(64);

CREATE UNIQUE INDEX IF NOT EXISTS idx_emails_tracking_id
    ON emails(tracking_id)
    WHERE tracking_id IS NOT NULL;

-- -----------------------------------------------------------
-- 2. Add SES message ID index (for bounce/delivery correlation)
-- -----------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_emails_ses_message_id
    ON emails(ses_message_id)
    WHERE ses_message_id IS NOT NULL;

-- -----------------------------------------------------------
-- 3. Add domain soft-delete support
-- -----------------------------------------------------------
ALTER TABLE domains
    ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS idx_domains_deleted
    ON domains(tenant_id, deleted_at)
    WHERE deleted_at IS NULL;

-- -----------------------------------------------------------
-- 4. Add API key rotation support
-- -----------------------------------------------------------
ALTER TABLE api_keys
    ADD COLUMN IF NOT EXISTS rotating_expires_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS replaced_by_key_id UUID REFERENCES api_keys(id);

-- -----------------------------------------------------------
-- 5. Add tags index for filtering (GIN index for array overlap)
-- -----------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_emails_tags
    ON emails USING GIN (tags);

-- -----------------------------------------------------------
-- 6. Add composite index for email log filtering by date range
-- -----------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_emails_tenant_date_range
    ON emails(tenant_id, created_at DESC, status);

-- -----------------------------------------------------------
-- 7. Add index for suppression email search
-- -----------------------------------------------------------
CREATE INDEX IF NOT EXISTS idx_suppression_email_pattern
    ON suppression_list(tenant_id, email_address varchar_pattern_ops);

-- -----------------------------------------------------------
-- 8. Create tracking_links table (for click tracking)
-- -----------------------------------------------------------
CREATE TABLE IF NOT EXISTS tracking_links (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email_id        UUID NOT NULL REFERENCES emails(id) ON DELETE CASCADE,
    original_url    TEXT NOT NULL,
    token           VARCHAR(64) NOT NULL,
    clicked_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_tracking_links_token UNIQUE (token)
);

CREATE INDEX IF NOT EXISTS idx_tracking_links_email ON tracking_links(email_id);
CREATE INDEX IF NOT EXISTS idx_tracking_links_token ON tracking_links(token);

COMMIT;
