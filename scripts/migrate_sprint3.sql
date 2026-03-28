-- Sprint 3 Migration: Webhook delivery logs table and analytics indexes
-- Date: 2026-03-27

BEGIN;

-- Create webhook_delivery_logs table
CREATE TABLE IF NOT EXISTS webhook_delivery_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    webhook_id UUID NOT NULL REFERENCES webhooks(id) ON DELETE CASCADE,
    email_id UUID NOT NULL REFERENCES emails(id) ON DELETE CASCADE,
    event_type VARCHAR(50) NOT NULL,
    status_code INTEGER NOT NULL DEFAULT 0,
    success BOOLEAN NOT NULL DEFAULT false,
    error_message VARCHAR(2000),
    attempt_number INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes for webhook_delivery_logs
CREATE INDEX IF NOT EXISTS idx_webhook_delivery_logs_webhook ON webhook_delivery_logs(webhook_id);
CREATE INDEX IF NOT EXISTS idx_webhook_delivery_logs_email ON webhook_delivery_logs(email_id);
CREATE INDEX IF NOT EXISTS idx_webhook_delivery_logs_created ON webhook_delivery_logs(created_at);

-- Analytics indexes for efficient aggregation queries
CREATE INDEX IF NOT EXISTS idx_emails_tenant_created ON emails(tenant_id, created_at);
CREATE INDEX IF NOT EXISTS idx_emails_status ON emails(status);
CREATE INDEX IF NOT EXISTS idx_emails_tenant_status_created ON emails(tenant_id, status, created_at);

COMMIT;
