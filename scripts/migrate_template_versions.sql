-- Template Versioning: version history and rollback support
-- Run after: migrate_sprint5.sql

CREATE TABLE IF NOT EXISTS template_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id UUID NOT NULL REFERENCES templates(id) ON DELETE CASCADE,
    version INT NOT NULL,
    name VARCHAR(200) NOT NULL,
    subject TEXT NOT NULL,
    html_body TEXT,
    text_body TEXT,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_template_versions_template_version
    ON template_versions (template_id, version DESC);

CREATE INDEX IF NOT EXISTS ix_template_versions_template_created
    ON template_versions (template_id, created_at DESC);
