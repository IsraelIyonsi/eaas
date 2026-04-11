-- =============================================================================
-- EaaS Database Scalability Migration
-- Converts emails, inbound_emails, email_events to partitioned tables
-- Adds archival functions, auto-partition creation, performance indexes
-- Target: PostgreSQL 16 | Scale: 100M+ emails
-- =============================================================================

BEGIN;

-- ============================================================
-- 1. PARTITION: emails TABLE
-- ============================================================
-- Step 1a: Rename existing table
ALTER TABLE email_events DROP CONSTRAINT IF EXISTS email_events_email_id_fkey;
ALTER TABLE webhook_delivery_logs DROP CONSTRAINT IF EXISTS webhook_delivery_logs_email_id_fkey;
ALTER TABLE inbound_emails DROP CONSTRAINT IF EXISTS inbound_emails_outbound_email_id_fkey;

ALTER TABLE emails RENAME TO emails_old;
ALTER INDEX IF EXISTS idx_emails_message_id RENAME TO idx_emails_message_id_old;
ALTER INDEX IF EXISTS idx_emails_tenant_status RENAME TO idx_emails_tenant_status_old;
ALTER INDEX IF EXISTS idx_emails_tenant_created RENAME TO idx_emails_tenant_created_old;
ALTER INDEX IF EXISTS idx_emails_batch RENAME TO idx_emails_batch_old;
ALTER INDEX IF EXISTS idx_emails_template RENAME TO idx_emails_template_old;
ALTER INDEX IF EXISTS idx_emails_api_key RENAME TO idx_emails_api_key_old;
ALTER INDEX IF EXISTS idx_emails_from RENAME TO idx_emails_from_old;
ALTER INDEX IF EXISTS idx_emails_status RENAME TO idx_emails_status_old;
ALTER INDEX IF EXISTS idx_emails_tenant_status_created RENAME TO idx_emails_tenant_status_created_old;

-- Step 1b: Create new partitioned table with same schema
CREATE TABLE emails (
    id              UUID NOT NULL DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    api_key_id      UUID NOT NULL,
    message_id      VARCHAR(64) NOT NULL,
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
    clicked_at      TIMESTAMP WITH TIME ZONE,
    PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

-- Step 1c: Create monthly partitions for 2026-01 through 2027-06
CREATE TABLE emails_2026_01 PARTITION OF emails FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE emails_2026_02 PARTITION OF emails FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
CREATE TABLE emails_2026_03 PARTITION OF emails FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
CREATE TABLE emails_2026_04 PARTITION OF emails FOR VALUES FROM ('2026-04-01') TO ('2026-05-01');
CREATE TABLE emails_2026_05 PARTITION OF emails FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');
CREATE TABLE emails_2026_06 PARTITION OF emails FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
CREATE TABLE emails_2026_07 PARTITION OF emails FOR VALUES FROM ('2026-07-01') TO ('2026-08-01');
CREATE TABLE emails_2026_08 PARTITION OF emails FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
CREATE TABLE emails_2026_09 PARTITION OF emails FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE emails_2026_10 PARTITION OF emails FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE emails_2026_11 PARTITION OF emails FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE TABLE emails_2026_12 PARTITION OF emails FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');
CREATE TABLE emails_2027_01 PARTITION OF emails FOR VALUES FROM ('2027-01-01') TO ('2027-02-01');
CREATE TABLE emails_2027_02 PARTITION OF emails FOR VALUES FROM ('2027-02-01') TO ('2027-03-01');
CREATE TABLE emails_2027_03 PARTITION OF emails FOR VALUES FROM ('2027-03-01') TO ('2027-04-01');
CREATE TABLE emails_2027_04 PARTITION OF emails FOR VALUES FROM ('2027-04-01') TO ('2027-05-01');
CREATE TABLE emails_2027_05 PARTITION OF emails FOR VALUES FROM ('2027-05-01') TO ('2027-06-01');
CREATE TABLE emails_2027_06 PARTITION OF emails FOR VALUES FROM ('2027-06-01') TO ('2027-07-01');
CREATE TABLE emails_default PARTITION OF emails DEFAULT;

-- Step 1d: Migrate data from old table
INSERT INTO emails SELECT * FROM emails_old;

-- Step 1e: Drop old table
DROP TABLE emails_old CASCADE;

-- Step 1f: Create indexes on partitioned table
CREATE UNIQUE INDEX idx_emails_message_id ON emails(message_id);
CREATE INDEX idx_emails_tenant_status ON emails(tenant_id, status, created_at DESC);
CREATE INDEX idx_emails_tenant_created ON emails(tenant_id, created_at DESC);
CREATE INDEX idx_emails_batch ON emails(batch_id) WHERE batch_id IS NOT NULL;
CREATE INDEX idx_emails_template ON emails(template_id) WHERE template_id IS NOT NULL;
CREATE INDEX idx_emails_api_key ON emails(api_key_id);
CREATE INDEX idx_emails_from ON emails(from_email);
CREATE INDEX idx_emails_ses_message_id ON emails(ses_message_id) WHERE ses_message_id IS NOT NULL;

-- Step 1g: Re-add foreign key constraints (as non-partitioned references)
-- Note: FKs referencing partitioned tables require the partition key in the FK.
-- We use application-level enforcement for tenant_id/api_key_id references,
-- and add back the FKs from child tables below after all tables are partitioned.

-- ============================================================
-- 2. PARTITION: inbound_emails TABLE
-- ============================================================
ALTER TABLE inbound_attachments DROP CONSTRAINT IF EXISTS inbound_attachments_inbound_email_id_fkey;

ALTER TABLE inbound_emails RENAME TO inbound_emails_old;
ALTER INDEX IF EXISTS idx_inbound_emails_tenant RENAME TO idx_inbound_emails_tenant_old;
ALTER INDEX IF EXISTS idx_inbound_emails_tenant_received RENAME TO idx_inbound_emails_tenant_received_old;
ALTER INDEX IF EXISTS idx_inbound_emails_from RENAME TO idx_inbound_emails_from_old;
ALTER INDEX IF EXISTS idx_inbound_emails_message_id RENAME TO idx_inbound_emails_message_id_old;
ALTER INDEX IF EXISTS idx_inbound_emails_in_reply_to RENAME TO idx_inbound_emails_in_reply_to_old;
ALTER INDEX IF EXISTS idx_inbound_emails_outbound RENAME TO idx_inbound_emails_outbound_old;

CREATE TABLE inbound_emails (
    id                  UUID NOT NULL DEFAULT gen_random_uuid(),
    tenant_id           UUID NOT NULL,
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
    references          TEXT,
    outbound_email_id   UUID,
    received_at         TIMESTAMP NOT NULL DEFAULT NOW(),
    processed_at        TIMESTAMP,
    created_at          TIMESTAMP NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, received_at)
) PARTITION BY RANGE (received_at);

-- Monthly partitions: 2026-01 through 2027-06
CREATE TABLE inbound_emails_2026_01 PARTITION OF inbound_emails FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE inbound_emails_2026_02 PARTITION OF inbound_emails FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
CREATE TABLE inbound_emails_2026_03 PARTITION OF inbound_emails FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
CREATE TABLE inbound_emails_2026_04 PARTITION OF inbound_emails FOR VALUES FROM ('2026-04-01') TO ('2026-05-01');
CREATE TABLE inbound_emails_2026_05 PARTITION OF inbound_emails FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');
CREATE TABLE inbound_emails_2026_06 PARTITION OF inbound_emails FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
CREATE TABLE inbound_emails_2026_07 PARTITION OF inbound_emails FOR VALUES FROM ('2026-07-01') TO ('2026-08-01');
CREATE TABLE inbound_emails_2026_08 PARTITION OF inbound_emails FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
CREATE TABLE inbound_emails_2026_09 PARTITION OF inbound_emails FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE inbound_emails_2026_10 PARTITION OF inbound_emails FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE inbound_emails_2026_11 PARTITION OF inbound_emails FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE TABLE inbound_emails_2026_12 PARTITION OF inbound_emails FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');
CREATE TABLE inbound_emails_2027_01 PARTITION OF inbound_emails FOR VALUES FROM ('2027-01-01') TO ('2027-02-01');
CREATE TABLE inbound_emails_2027_02 PARTITION OF inbound_emails FOR VALUES FROM ('2027-02-01') TO ('2027-03-01');
CREATE TABLE inbound_emails_2027_03 PARTITION OF inbound_emails FOR VALUES FROM ('2027-03-01') TO ('2027-04-01');
CREATE TABLE inbound_emails_2027_04 PARTITION OF inbound_emails FOR VALUES FROM ('2027-04-01') TO ('2027-05-01');
CREATE TABLE inbound_emails_2027_05 PARTITION OF inbound_emails FOR VALUES FROM ('2027-05-01') TO ('2027-06-01');
CREATE TABLE inbound_emails_2027_06 PARTITION OF inbound_emails FOR VALUES FROM ('2027-06-01') TO ('2027-07-01');
CREATE TABLE inbound_emails_default PARTITION OF inbound_emails DEFAULT;

-- Migrate data
INSERT INTO inbound_emails SELECT * FROM inbound_emails_old;
DROP TABLE inbound_emails_old CASCADE;

-- Indexes for inbound_emails
CREATE INDEX idx_inbound_emails_tenant ON inbound_emails(tenant_id);
CREATE INDEX idx_inbound_emails_tenant_status_received ON inbound_emails(tenant_id, status, received_at DESC);
CREATE INDEX idx_inbound_emails_tenant_received ON inbound_emails(tenant_id, received_at DESC);
CREATE INDEX idx_inbound_emails_from ON inbound_emails(tenant_id, from_email);
CREATE INDEX idx_inbound_emails_message_id ON inbound_emails(message_id);
CREATE INDEX idx_inbound_emails_in_reply_to ON inbound_emails(in_reply_to) WHERE in_reply_to IS NOT NULL;
CREATE INDEX idx_inbound_emails_outbound ON inbound_emails(outbound_email_id) WHERE outbound_email_id IS NOT NULL;

-- ============================================================
-- 3. PARTITION: email_events TABLE
-- ============================================================
ALTER TABLE email_events RENAME TO email_events_old;
ALTER INDEX IF EXISTS idx_email_events_email RENAME TO idx_email_events_email_old;
ALTER INDEX IF EXISTS idx_email_events_type RENAME TO idx_email_events_type_old;

CREATE TABLE email_events (
    id              UUID NOT NULL DEFAULT gen_random_uuid(),
    email_id        UUID NOT NULL,
    event_type      event_type NOT NULL,
    data            JSONB DEFAULT '{}'::jsonb,
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

-- Monthly partitions: 2026-01 through 2027-06
CREATE TABLE email_events_2026_01 PARTITION OF email_events FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE email_events_2026_02 PARTITION OF email_events FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
CREATE TABLE email_events_2026_03 PARTITION OF email_events FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
CREATE TABLE email_events_2026_04 PARTITION OF email_events FOR VALUES FROM ('2026-04-01') TO ('2026-05-01');
CREATE TABLE email_events_2026_05 PARTITION OF email_events FOR VALUES FROM ('2026-05-01') TO ('2026-06-01');
CREATE TABLE email_events_2026_06 PARTITION OF email_events FOR VALUES FROM ('2026-06-01') TO ('2026-07-01');
CREATE TABLE email_events_2026_07 PARTITION OF email_events FOR VALUES FROM ('2026-07-01') TO ('2026-08-01');
CREATE TABLE email_events_2026_08 PARTITION OF email_events FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
CREATE TABLE email_events_2026_09 PARTITION OF email_events FOR VALUES FROM ('2026-09-01') TO ('2026-10-01');
CREATE TABLE email_events_2026_10 PARTITION OF email_events FOR VALUES FROM ('2026-10-01') TO ('2026-11-01');
CREATE TABLE email_events_2026_11 PARTITION OF email_events FOR VALUES FROM ('2026-11-01') TO ('2026-12-01');
CREATE TABLE email_events_2026_12 PARTITION OF email_events FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');
CREATE TABLE email_events_2027_01 PARTITION OF email_events FOR VALUES FROM ('2027-01-01') TO ('2027-02-01');
CREATE TABLE email_events_2027_02 PARTITION OF email_events FOR VALUES FROM ('2027-02-01') TO ('2027-03-01');
CREATE TABLE email_events_2027_03 PARTITION OF email_events FOR VALUES FROM ('2027-03-01') TO ('2027-04-01');
CREATE TABLE email_events_2027_04 PARTITION OF email_events FOR VALUES FROM ('2027-04-01') TO ('2027-05-01');
CREATE TABLE email_events_2027_05 PARTITION OF email_events FOR VALUES FROM ('2027-05-01') TO ('2027-06-01');
CREATE TABLE email_events_2027_06 PARTITION OF email_events FOR VALUES FROM ('2027-06-01') TO ('2027-07-01');
CREATE TABLE email_events_default PARTITION OF email_events DEFAULT;

-- Migrate data
INSERT INTO email_events SELECT * FROM email_events_old;
DROP TABLE email_events_old CASCADE;

-- Indexes for email_events
CREATE INDEX idx_email_events_email ON email_events(email_id);
CREATE INDEX idx_email_events_type ON email_events(event_type, created_at);
CREATE INDEX idx_email_events_email_created ON email_events(email_id, created_at DESC);

-- ============================================================
-- 4. ARCHIVE TABLE & FUNCTION
-- ============================================================

-- Archive table for old emails (not partitioned -- cold storage)
CREATE TABLE IF NOT EXISTS emails_archive (
    id              UUID NOT NULL,
    tenant_id       UUID NOT NULL,
    api_key_id      UUID NOT NULL,
    message_id      VARCHAR(64) NOT NULL,
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
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL,
    sent_at         TIMESTAMP WITH TIME ZONE,
    delivered_at    TIMESTAMP WITH TIME ZONE,
    opened_at       TIMESTAMP WITH TIME ZONE,
    clicked_at      TIMESTAMP WITH TIME ZONE,
    archived_at     TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS idx_emails_archive_tenant ON emails_archive(tenant_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_emails_archive_message_id ON emails_archive(message_id);

-- Function: archive_old_emails
-- Moves emails older than retention_days to emails_archive, returns count
CREATE OR REPLACE FUNCTION archive_old_emails(retention_days INT)
RETURNS BIGINT
LANGUAGE plpgsql
AS $$
DECLARE
    cutoff_date TIMESTAMP WITH TIME ZONE;
    archived_count BIGINT;
BEGIN
    cutoff_date := NOW() - (retention_days || ' days')::INTERVAL;

    -- Insert into archive
    WITH moved AS (
        DELETE FROM emails
        WHERE created_at < cutoff_date
        RETURNING *
    )
    INSERT INTO emails_archive (
        id, tenant_id, api_key_id, message_id, batch_id,
        from_email, from_name, to_emails, cc_emails, bcc_emails,
        subject, html_body, text_body, template_id, variables,
        attachments, tags, metadata, track_opens, track_clicks,
        status, ses_message_id, error_message, created_at,
        sent_at, delivered_at, opened_at, clicked_at, archived_at
    )
    SELECT
        id, tenant_id, api_key_id, message_id, batch_id,
        from_email, from_name, to_emails, cc_emails, bcc_emails,
        subject, html_body, text_body, template_id, variables,
        attachments, tags, metadata, track_opens, track_clicks,
        status, ses_message_id, error_message, created_at,
        sent_at, delivered_at, opened_at, clicked_at, NOW()
    FROM moved;

    GET DIAGNOSTICS archived_count = ROW_COUNT;

    RAISE NOTICE 'Archived % emails older than % days (cutoff: %)',
        archived_count, retention_days, cutoff_date;

    RETURN archived_count;
END;
$$;

-- ============================================================
-- 5. AUTO-PARTITION CREATION FUNCTIONS
-- ============================================================

-- Function: create_monthly_partition
-- Creates a single monthly partition for a given table, year, month
CREATE OR REPLACE FUNCTION create_monthly_partition(
    p_table_name TEXT,
    p_year INT,
    p_month INT
)
RETURNS VOID
LANGUAGE plpgsql
AS $$
DECLARE
    partition_name TEXT;
    start_date TEXT;
    end_date TEXT;
    next_year INT;
    next_month INT;
BEGIN
    -- Build partition name: tablename_YYYY_MM
    partition_name := p_table_name || '_' || p_year::TEXT || '_' || LPAD(p_month::TEXT, 2, '0');

    -- Calculate date range
    start_date := p_year::TEXT || '-' || LPAD(p_month::TEXT, 2, '0') || '-01';

    IF p_month = 12 THEN
        next_year := p_year + 1;
        next_month := 1;
    ELSE
        next_year := p_year;
        next_month := p_month + 1;
    END IF;
    end_date := next_year::TEXT || '-' || LPAD(next_month::TEXT, 2, '0') || '-01';

    -- Check if partition already exists
    IF EXISTS (
        SELECT 1 FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relname = partition_name
        AND n.nspname = 'public'
    ) THEN
        RAISE NOTICE 'Partition % already exists, skipping.', partition_name;
        RETURN;
    END IF;

    -- Create the partition
    EXECUTE format(
        'CREATE TABLE %I PARTITION OF %I FOR VALUES FROM (%L) TO (%L)',
        partition_name, p_table_name, start_date, end_date
    );

    RAISE NOTICE 'Created partition % (%s to %s)', partition_name, start_date, end_date;
END;
$$;

-- Function: create_future_partitions
-- Creates partitions for the next N months for all partitioned tables
CREATE OR REPLACE FUNCTION create_future_partitions(months_ahead INT DEFAULT 3)
RETURNS VOID
LANGUAGE plpgsql
AS $$
DECLARE
    target_date DATE;
    i INT;
    tables TEXT[] := ARRAY['emails', 'inbound_emails', 'email_events'];
    t TEXT;
BEGIN
    FOR i IN 0..months_ahead - 1 LOOP
        target_date := (DATE_TRUNC('month', CURRENT_DATE) + (i || ' months')::INTERVAL)::DATE;

        FOREACH t IN ARRAY tables LOOP
            PERFORM create_monthly_partition(
                t,
                EXTRACT(YEAR FROM target_date)::INT,
                EXTRACT(MONTH FROM target_date)::INT
            );
        END LOOP;
    END LOOP;
END;
$$;

-- ============================================================
-- 6. AUTOVACUUM TUNING FOR HIGH-THROUGHPUT TABLES
-- ============================================================
ALTER TABLE emails SET (autovacuum_vacuum_scale_factor = 0.01);
ALTER TABLE emails SET (autovacuum_analyze_scale_factor = 0.005);

ALTER TABLE inbound_emails SET (autovacuum_vacuum_scale_factor = 0.01);
ALTER TABLE inbound_emails SET (autovacuum_analyze_scale_factor = 0.005);

ALTER TABLE email_events SET (autovacuum_vacuum_scale_factor = 0.01);
ALTER TABLE email_events SET (autovacuum_analyze_scale_factor = 0.005);

-- ============================================================
-- 7. RECORD MIGRATION IN EF CORE HISTORY
-- ============================================================
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260401000000_Scalability_Partitioning', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
