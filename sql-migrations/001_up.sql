CREATE TABLE users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email           text NOT NULL,
    password_hash   text NOT NULL,
    password_algo   text NOT NULL DEFAULT 'argon2id',
    email_verified  boolean NOT NULL DEFAULT false,
    role            text NOT NULL DEFAULT 'user'
                      CHECK (role IN ('user','admin')),
    status          text NOT NULL DEFAULT 'active'
                      CHECK (status IN ('active','suspended','pending')),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean NOT NULL DEFAULT false,
    deleted_at      timestamptz
);
CREATE UNIQUE INDEX ux_users_email_lower ON users (lower(email))
    WHERE is_deleted = false;
