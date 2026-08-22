CREATE TABLE self_reports (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    value       smallint NOT NULL CHECK (value BETWEEN 1 AND 5),
    recorded_at timestamptz NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_self_reports_user_recorded ON self_reports(user_id, recorded_at);

CREATE TABLE breathing_sessions (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    started_at  timestamptz NOT NULL,
    ended_at    timestamptz NOT NULL CHECK (ended_at > started_at),
    hr_before   numeric CHECK (hr_before > 0),
    hr_after    numeric CHECK (hr_after > 0),
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_breathing_sessions_user_started ON breathing_sessions(user_id, started_at);

CREATE TABLE experiments (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES users(id),
    name            text NOT NULL,
    duration_days   integer NOT NULL CHECK (duration_days > 0),
    started_at      timestamptz NOT NULL,
    ended_at        timestamptz CHECK (ended_at IS NULL OR ended_at > started_at),
    baseline_metric jsonb NOT NULL DEFAULT '{}',
    result_metric   jsonb NOT NULL DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_experiments_user_started ON experiments(user_id, started_at);
