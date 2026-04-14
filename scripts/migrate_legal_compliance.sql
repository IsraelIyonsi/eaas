-- =============================================================================
-- Legal Compliance Migration (#41, #42)
-- Adds CAN-SPAM §7704(a)(5) required columns: legal_entity_name, postal_address.
-- Both are nullable at the DB layer for backward compatibility with the 7
-- existing live tenants; RegisterHandler / CreateTenantHandler enforce
-- required-on-write through FluentValidation.
-- =============================================================================

BEGIN;

ALTER TABLE tenants
    ADD COLUMN IF NOT EXISTS legal_entity_name VARCHAR(255);

ALTER TABLE tenants
    ADD COLUMN IF NOT EXISTS postal_address TEXT;

COMMIT;

-- Backfill reminder: existing tenants must have legal_entity_name and
-- postal_address populated before any further marketing email campaigns.
-- Run manually for each tenant once addresses are collected:
--
--   UPDATE tenants
--   SET legal_entity_name = $1,
--       postal_address = $2,
--       updated_at = NOW()
--   WHERE id = $3;
