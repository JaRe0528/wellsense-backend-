CREATE TABLE refresh_tokens (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 uuid NOT NULL REFERENCES users(id),
    token_hash              text NOT NULL UNIQUE,
    expires_at              timestamptz NOT NULL,
    revoked_at              timestamptz,
    replaced_by_token_id    uuid REFERENCES refresh_tokens(id),
    created_by_ip           inet,
    created_at              timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE INDEX ix_refresh_tokens_expires_at ON refresh_tokens(expires_at)
    WHERE revoked_at IS NULL;

CREATE TABLE email_verification_tokens (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    token_hash  text NOT NULL UNIQUE,
    expires_at  timestamptz NOT NULL,
    used_at     timestamptz
);
CREATE INDEX ix_evt_user_id ON email_verification_tokens(user_id);

CREATE TABLE password_reset_tokens (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    token_hash  text NOT NULL UNIQUE,
    expires_at  timestamptz NOT NULL,
    used_at     timestamptz
);
CREATE INDEX ix_prt_user_id ON password_reset_tokens(user_id);

CREATE TABLE audit_logs (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid REFERENCES users(id),
    action      text NOT NULL,
    metadata    jsonb NOT NULL DEFAULT '{}',
    ip_address  inet,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_audit_logs_user_id ON audit_logs(user_id);
CREATE INDEX ix_audit_logs_created_at ON audit_logs(created_at);
CREATE INDEX ix_audit_logs_metadata_gin ON audit_logs USING gin(metadata);
