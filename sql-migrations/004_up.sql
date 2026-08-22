CREATE TABLE devices (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    type        text NOT NULL CHECK (type IN ('PHONE','WATCH')),
    model       text,
    os_version  text,
    app_version text,
    last_seen_at timestamptz,
    status      text NOT NULL DEFAULT 'ACTIVE'
                  CHECK (status IN ('ACTIVE','INACTIVE','UNPAIRED')),
    paired_at   timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_devices_user_id ON devices(user_id);
CREATE INDEX ix_devices_user_type ON devices(user_id, type);
