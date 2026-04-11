-- Migration: Add scheduled email support
-- Date: 2026-04-03

ALTER TABLE emails ADD COLUMN IF NOT EXISTS scheduled_at TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS ix_emails_scheduled
    ON emails (status, scheduled_at)
    WHERE status = 'scheduled';
