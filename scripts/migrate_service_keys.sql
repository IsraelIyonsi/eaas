-- Migration: Add is_service_key flag for dashboard-to-API tenant impersonation
-- Service keys can pass X-Tenant-Id header to act on behalf of any tenant.
-- This enables the Vercel-hosted dashboard proxy to authenticate API requests
-- for whichever tenant is currently logged in.

ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS is_service_key BOOLEAN NOT NULL DEFAULT FALSE;

-- Ensure the platform tenant exists (seed may not have run on this DB)
INSERT INTO tenants (id, name)
VALUES ('00000000-0000-0000-0000-000000000001', 'Default')
ON CONFLICT (id) DO NOTHING;

-- Upsert the dashboard service key with the correct hash
-- Plaintext: eaas_live_devkey00000000000000000000000000000000
-- SHA-256:   37c93dce3286b0864c1b75f06133833523d1c1f241e8c48c60f040bb3024acb8
INSERT INTO api_keys (id, tenant_id, name, key_hash, prefix, status, is_service_key)
VALUES (
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    'Dashboard Service Key',
    '37c93dce3286b0864c1b75f06133833523d1c1f241e8c48c60f040bb3024acb8',
    'eaas_liv',
    'active',
    TRUE
)
ON CONFLICT (id) DO UPDATE SET
    key_hash = EXCLUDED.key_hash,
    is_service_key = TRUE,
    status = 'active';

-- Reset admin password to 'admin' (temporary — change after first login)
UPDATE admin_users
SET password_hash = '$2b$12$zS1Falh.9c0YS31qXy.2K.pMqpbVseBXxNAP8IxmK6uyW6OaldEnK',
    updated_at = NOW()
WHERE email = 'admin@eaas.local';
