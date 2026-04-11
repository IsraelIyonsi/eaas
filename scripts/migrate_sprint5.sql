-- Sprint 5: Admin Dashboard Foundation
-- Migration script for PostgreSQL
-- Creates: 3 enum types, 2 tables, alters tenants table, seeds SuperAdmin

BEGIN;

-- Enum types (lowercase values to match Npgsql convention)
DO $$ BEGIN
    CREATE TYPE admin_role AS ENUM ('super_admin', 'admin', 'read_only');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE tenant_status AS ENUM ('active', 'suspended', 'deactivated');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE audit_action AS ENUM (
        'tenant_created', 'tenant_updated', 'tenant_suspended',
        'tenant_activated', 'tenant_deactivated',
        'admin_user_created', 'admin_user_updated', 'admin_user_deleted',
        'admin_login', 'admin_login_failed', 'settings_updated'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

-- Table: admin_users
CREATE TABLE IF NOT EXISTS admin_users (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email               VARCHAR(255) NOT NULL,
    display_name        VARCHAR(255) NOT NULL,
    password_hash       VARCHAR(512) NOT NULL,
    role                admin_role NOT NULL DEFAULT 'admin',
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    last_login_at       TIMESTAMP,
    created_at          TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_admin_users_email ON admin_users(email);

-- Table: audit_logs
CREATE TABLE IF NOT EXISTS audit_logs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id       UUID NOT NULL REFERENCES admin_users(id) ON DELETE RESTRICT,
    action              audit_action NOT NULL,
    target_type         VARCHAR(100),
    target_id           VARCHAR(255),
    details             JSONB DEFAULT '{}'::jsonb,
    ip_address          VARCHAR(45),
    created_at          TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_audit_logs_admin_user ON audit_logs(admin_user_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_action ON audit_logs(action);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs(created_at DESC);

-- Alter tenants table: add new columns
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS status tenant_status NOT NULL DEFAULT 'active';
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS contact_email VARCHAR(255);
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS company_name VARCHAR(255);
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS max_api_keys INT;
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS max_domains_count INT;
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS monthly_email_limit BIGINT;
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS notes TEXT;

-- Seed SuperAdmin user
-- Password: 'ChangeMeOnFirstLogin!' hashed with BCrypt (cost 12)
-- IMPORTANT: Change this password immediately after first deployment
INSERT INTO admin_users (id, email, display_name, password_hash, role, is_active, created_at, updated_at)
VALUES (
    '00000000-0000-0000-0000-000000000100',
    'admin@eaas.local',
    'Super Admin',
    '$2a$12$LJ3m4ys3Lk0TSwMCfVGZy.VzwG1DxRjVMqPqH3.YzVJh0FVC3e2me',
    'super_admin',
    TRUE,
    NOW(),
    NOW()
)
ON CONFLICT (id) DO NOTHING;

COMMIT;
