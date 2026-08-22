CREATE TABLE device_link_codes (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       uuid NOT NULL REFERENCES users(id),
    code_hash     text NOT NULL,
    attempts      integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    max_attempts  integer NOT NULL DEFAULT 5 CHECK (max_attempts > 0),
    expires_at    timestamptz NOT NULL,
    used_at       timestamptz,
    device_id     uuid REFERENCES devices(id),
    created_at    timestamptz NOT NULL DEFAULT now(),
    CHECK (attempts <= max_attempts),
    CHECK (expires_at > created_at),
    CHECK ((used_at IS NULL) = (device_id IS NULL))
);

CREATE UNIQUE INDEX ux_device_link_codes_one_active_per_user
    ON device_link_codes(user_id) WHERE used_at IS NULL;

CREATE UNIQUE INDEX ux_device_link_codes_active_code_hash
    ON device_link_codes(code_hash) WHERE used_at IS NULL;

CREATE INDEX ix_device_link_codes_expires_at
    ON device_link_codes(expires_at) WHERE used_at IS NULL;
