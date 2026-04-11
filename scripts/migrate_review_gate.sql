-- Review Gate Migration
-- Adds missing PG enum types, converts varchar columns to native enums,
-- and adds missing CreatedAt columns identified during the review gate.
-- Idempotent: safe to run multiple times.

BEGIN;

-- 1. Create missing PostgreSQL enum types
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'dns_record_type') THEN
    CREATE TYPE dns_record_type AS ENUM ('txt', 'cname', 'mx');
  END IF;
END $$;

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'invoice_status') THEN
    CREATE TYPE invoice_status AS ENUM ('pending', 'paid', 'failed', 'refunded');
  END IF;
END $$;

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'webhook_status') THEN
    CREATE TYPE webhook_status AS ENUM ('active', 'inactive', 'failed');
  END IF;
END $$;

-- 2. Convert webhooks.status from varchar to native enum
DO $$ BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_name = 'webhooks' AND column_name = 'status' AND data_type = 'character varying'
  ) THEN
    ALTER TABLE webhooks ALTER COLUMN status DROP DEFAULT;
    ALTER TABLE webhooks ALTER COLUMN status TYPE webhook_status USING status::webhook_status;
    ALTER TABLE webhooks ALTER COLUMN status SET DEFAULT 'active';
  END IF;
END $$;

-- 3. Convert invoices.status from varchar to native enum
DO $$ BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_name = 'invoices' AND column_name = 'status' AND data_type = 'character varying'
  ) THEN
    ALTER TABLE invoices ALTER COLUMN status DROP DEFAULT;
    ALTER TABLE invoices ALTER COLUMN status TYPE invoice_status USING status::invoice_status;
    ALTER TABLE invoices ALTER COLUMN status SET DEFAULT 'pending';
  END IF;
END $$;

-- 4. Convert dns_records.record_type from varchar to native enum
DO $$ BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_name = 'dns_records' AND column_name = 'record_type' AND data_type = 'character varying'
  ) THEN
    ALTER TABLE dns_records ALTER COLUMN record_type DROP DEFAULT;
    ALTER TABLE dns_records ALTER COLUMN record_type TYPE dns_record_type USING LOWER(record_type)::dns_record_type;
    ALTER TABLE dns_records ALTER COLUMN record_type SET DEFAULT 'txt';
  END IF;
END $$;

-- 5. Add missing created_at columns
ALTER TABLE dns_records ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW();
ALTER TABLE suppression_list ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

COMMIT;
