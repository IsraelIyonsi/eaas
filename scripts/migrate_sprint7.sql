-- Sprint 7: Customer Registration
-- Adds password_hash to tenants for customer self-service signup

-- Add password_hash column (nullable for backward compatibility with admin-created tenants)
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS password_hash TEXT;

-- Add index on contact_email for login lookups (case-insensitive)
CREATE INDEX IF NOT EXISTS ix_tenants_contact_email ON tenants (LOWER(contact_email));
