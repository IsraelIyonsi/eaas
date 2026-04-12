-- EaaS Development Seed Data
-- WARNING: This file is for local development ONLY.
-- It is mounted via docker-compose.override.yml and must NEVER be used in production.

-- Default tenant for Sprint 1
INSERT INTO tenants (id, name) VALUES (
    '00000000-0000-0000-0000-000000000001',
    'Default'
) ON CONFLICT (id) DO NOTHING;

-- Dev-only API key for local development
-- Plaintext key: eaas_live_devkey00000000000000000000000000000000
-- SHA-256 hash of the above key
INSERT INTO api_keys (id, tenant_id, name, key_hash, prefix, status, is_service_key)
VALUES (
    '00000000-0000-0000-0000-000000000002',
    '00000000-0000-0000-0000-000000000001',
    'Development Key',
    '37c93dce3286b0864c1b75f06133833523d1c1f241e8c48c60f040bb3024acb8',
    'eaas_liv',
    'active',
    TRUE
) ON CONFLICT (id) DO NOTHING;
