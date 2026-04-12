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
    'b0c4de8f2a1b3c5d7e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d',
    'eaas_liv',
    'active',
    TRUE
) ON CONFLICT (id) DO NOTHING;
