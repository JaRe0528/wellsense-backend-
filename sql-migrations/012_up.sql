CREATE TABLE membership_plans (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code        text NOT NULL UNIQUE CHECK (code IN ('FREE','BASIC','PRO','PROFESSIONAL')),
    name        text NOT NULL,
    price_cents integer NOT NULL CHECK (price_cents >= 0),
    currency    char(3) NOT NULL DEFAULT 'MXN',
    features    jsonb NOT NULL DEFAULT '{}'
);

CREATE TABLE subscriptions (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    plan_id     uuid NOT NULL REFERENCES membership_plans(id),
    status      text NOT NULL DEFAULT 'ACTIVE'
                  CHECK (status IN ('ACTIVE','CANCELED','EXPIRED')),
    started_at  timestamptz NOT NULL DEFAULT now(),
    ends_at     timestamptz
);
CREATE INDEX ix_subscriptions_user_id ON subscriptions(user_id);
CREATE UNIQUE INDEX ux_subscriptions_one_active_per_user ON subscriptions(user_id)
    WHERE status = 'ACTIVE';

CREATE TABLE payments (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES users(id),
    plan_id         uuid NOT NULL REFERENCES membership_plans(id),
    subscription_id uuid REFERENCES subscriptions(id),
    amount_cents    integer NOT NULL CHECK (amount_cents > 0),
    currency        char(3) NOT NULL,
    status          text NOT NULL CHECK (status IN ('APPROVED','DECLINED')),
    card_brand      text,
    card_last4      char(4),
    transaction_id  text NOT NULL UNIQUE,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CHECK (subscription_id IS NULL OR status = 'APPROVED')
);
CREATE INDEX ix_payments_subscription_id ON payments(subscription_id) WHERE subscription_id IS NOT NULL;
CREATE INDEX ix_payments_plan_id ON payments(plan_id);
CREATE INDEX ix_payments_plan_status ON payments(plan_id, status);
CREATE INDEX ix_payments_user_created ON payments(user_id, created_at);

-- seed de los 4 planes (requerido por §migraciones HANDOFF-DB)
INSERT INTO membership_plans (code, name, price_cents, currency, features) VALUES
    ('FREE', 'Free', 0, 'MXN', '{}'),
    ('BASIC', 'Basic', 9900, 'MXN', '{}'),
    ('PRO', 'Pro', 19900, 'MXN', '{}'),
    ('PROFESSIONAL', 'Professional', 39900, 'MXN', '{}');
