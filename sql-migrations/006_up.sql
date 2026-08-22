CREATE TABLE measurements (
    id          uuid NOT NULL,
    user_id     uuid NOT NULL REFERENCES users(id),
    device_id   uuid NOT NULL REFERENCES devices(id),
    type        text NOT NULL
                  CHECK (type IN ('HEART_RATE','STEPS','SPO2','CALORIES','SKIN_TEMP')),
    value       numeric NOT NULL,
    unit        text NOT NULL,
    recorded_at timestamptz NOT NULL,
    synced_at   timestamptz,
    created_at  timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id, recorded_at)
) PARTITION BY RANGE (recorded_at);

CREATE UNIQUE INDEX ux_measurements_device_event ON measurements(device_id, id, recorded_at);
CREATE INDEX ix_measurements_user_recorded ON measurements(user_id, recorded_at);

-- partición inicial (mes de despliegue) — el resto las crea el job de pg_partman (Chat DevSecOps)
CREATE TABLE measurements_2026_08 PARTITION OF measurements
    FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
