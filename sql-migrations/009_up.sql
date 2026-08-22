CREATE TABLE wellness_scores (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    date        date NOT NULL,
    score       numeric NOT NULL CHECK (score BETWEEN 0 AND 100),
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, date)
);
CREATE INDEX ix_wellness_scores_user_date ON wellness_scores(user_id, date);

CREATE TABLE stress_scores (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    date        date NOT NULL,
    score       numeric NOT NULL CHECK (score BETWEEN 0 AND 100),
    level       text NOT NULL CHECK (level IN ('LOW','MEDIUM','HIGH')),
    confidence  numeric NOT NULL CHECK (confidence BETWEEN 0 AND 1),
    factors     jsonb NOT NULL DEFAULT '{}',
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, date)
);
CREATE INDEX ix_stress_scores_user_date ON stress_scores(user_id, date);

CREATE TABLE ml_predictions (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       uuid NOT NULL REFERENCES users(id),
    model_version text NOT NULL,
    type          text NOT NULL,
    input         jsonb NOT NULL,
    output        jsonb NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_ml_predictions_user_created ON ml_predictions(user_id, created_at);
CREATE INDEX ix_ml_predictions_model_version ON ml_predictions(model_version);
