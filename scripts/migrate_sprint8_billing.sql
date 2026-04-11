-- Sprint 8: Billing Database Foundation
-- Creates billing enums, plans, subscriptions, and invoices tables

-- Create billing enums
DO $$ BEGIN
    CREATE TYPE payment_provider AS ENUM ('stripe', 'paystack', 'flutterwave', 'paypal');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE plan_tier AS ENUM ('free', 'starter', 'pro', 'business', 'enterprise');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

DO $$ BEGIN
    CREATE TYPE subscription_status AS ENUM ('trial', 'active', 'past_due', 'cancelled', 'expired');
EXCEPTION WHEN duplicate_object THEN null;
END $$;

-- Plans table
CREATE TABLE IF NOT EXISTS plans (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    tier plan_tier NOT NULL DEFAULT 'free',
    monthly_price_usd DECIMAL(10,2) NOT NULL DEFAULT 0,
    annual_price_usd DECIMAL(10,2) NOT NULL DEFAULT 0,
    daily_email_limit INT NOT NULL DEFAULT 100,
    monthly_email_limit BIGINT NOT NULL DEFAULT 3000,
    max_api_keys INT NOT NULL DEFAULT 3,
    max_domains INT NOT NULL DEFAULT 2,
    max_templates INT NOT NULL DEFAULT 10,
    max_webhooks INT NOT NULL DEFAULT 5,
    custom_domain_branding BOOLEAN NOT NULL DEFAULT false,
    priority_support BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_plans_name ON plans (name);
CREATE INDEX IF NOT EXISTS ix_plans_tier_active ON plans (tier, is_active);

-- Subscriptions table
CREATE TABLE IF NOT EXISTS subscriptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    plan_id UUID NOT NULL REFERENCES plans(id),
    status subscription_status NOT NULL DEFAULT 'trial',
    provider payment_provider NOT NULL DEFAULT 'stripe',
    external_subscription_id TEXT,
    external_customer_id TEXT,
    current_period_start TIMESTAMPTZ NOT NULL,
    current_period_end TIMESTAMPTZ NOT NULL,
    cancelled_at TIMESTAMPTZ,
    trial_ends_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_subscriptions_tenant_status ON subscriptions (tenant_id, status);
CREATE INDEX IF NOT EXISTS ix_subscriptions_period_end ON subscriptions (current_period_end);

-- Invoices table
CREATE TABLE IF NOT EXISTS invoices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscription_id UUID NOT NULL REFERENCES subscriptions(id),
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    invoice_number VARCHAR(50) NOT NULL,
    amount_usd DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    provider payment_provider NOT NULL,
    external_invoice_id TEXT,
    external_payment_id TEXT,
    payment_method TEXT,
    period_start TIMESTAMPTZ NOT NULL,
    period_end TIMESTAMPTZ NOT NULL,
    paid_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_invoices_number ON invoices (invoice_number);
CREATE INDEX IF NOT EXISTS ix_invoices_tenant_created ON invoices (tenant_id, created_at DESC);

-- Seed default plans
INSERT INTO plans (name, tier, monthly_price_usd, annual_price_usd, daily_email_limit, monthly_email_limit, max_api_keys, max_domains, max_templates, max_webhooks, custom_domain_branding, priority_support)
VALUES
    ('Free', 'free', 0, 0, 100, 3000, 3, 2, 10, 5, false, false),
    ('Starter', 'starter', 9.99, 99.99, 1000, 30000, 5, 5, 50, 10, false, false),
    ('Pro', 'pro', 29.99, 299.99, 10000, 300000, 10, 10, 200, 25, true, false),
    ('Business', 'business', 79.99, 799.99, 50000, 1500000, 25, 25, 500, 50, true, true),
    ('Enterprise', 'enterprise', 199.99, 1999.99, 200000, 6000000, 100, 100, 2000, 200, true, true)
ON CONFLICT (name) DO NOTHING;
