-- Migration: Add is_service_key flag for dashboard-to-API tenant impersonation
-- Service keys can pass X-Tenant-Id header to act on behalf of any tenant.
-- This enables the Vercel-hosted dashboard proxy to authenticate API requests
-- for whichever tenant is currently logged in.

ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS is_service_key BOOLEAN NOT NULL DEFAULT FALSE;

-- Mark the seed dev key as a service key (used by dashboard proxy)
UPDATE api_keys SET is_service_key = TRUE
WHERE id = '00000000-0000-0000-0000-000000000002';
