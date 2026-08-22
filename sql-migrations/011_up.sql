CREATE TABLE notification_tokens (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    device_id   uuid NOT NULL REFERENCES devices(id),
    fcm_token   text NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (device_id, fcm_token)
);
CREATE INDEX ix_notification_tokens_user_id ON notification_tokens(user_id);

CREATE TABLE notifications (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    type        text NOT NULL,
    title       text NOT NULL,
    body        text NOT NULL,
    read_at     timestamptz,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_notifications_user_unread ON notifications(user_id, created_at)
    WHERE read_at IS NULL;

CREATE TABLE reminders (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           uuid NOT NULL REFERENCES users(id),
    type              text NOT NULL CHECK (type IN ('MANUAL','AUTO')),
    message           text NOT NULL,
    scheduled_at      timestamptz NOT NULL,
    cooldown_minutes  integer NOT NULL DEFAULT 0 CHECK (cooldown_minutes >= 0),
    created_at        timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_reminders_user_scheduled ON reminders(user_id, scheduled_at);
