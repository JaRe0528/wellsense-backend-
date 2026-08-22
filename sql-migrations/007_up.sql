CREATE TABLE sleep_sessions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES users(id),
    start_at        timestamptz NOT NULL,
    end_at          timestamptz NOT NULL CHECK (end_at > start_at),
    duration_minutes integer GENERATED ALWAYS AS
                      (round(extract(epoch FROM (end_at - start_at)) / 60)::integer) STORED,
    stages          jsonb NOT NULL DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_sleep_sessions_user_start ON sleep_sessions(user_id, start_at);

CREATE TABLE activity_sessions (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    type        text NOT NULL,
    start_at    timestamptz NOT NULL,
    end_at      timestamptz NOT NULL CHECK (end_at > start_at),
    steps       integer CHECK (steps >= 0),
    distance_m  numeric CHECK (distance_m >= 0),
    calories    numeric CHECK (calories >= 0),
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_activity_sessions_user_start ON activity_sessions(user_id, start_at);
