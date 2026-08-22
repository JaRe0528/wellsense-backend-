CREATE TABLE sync_operations (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id         uuid NOT NULL REFERENCES devices(id),
    request_id        text NOT NULL,
    status            text NOT NULL DEFAULT 'PROCESSING'
                        CHECK (status IN ('PROCESSING','COMPLETED','FAILED')),
    accepted_count    integer NOT NULL DEFAULT 0,
    duplicated_count  integer NOT NULL DEFAULT 0,
    rejected_count    integer NOT NULL DEFAULT 0,
    created_at        timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_sync_operations_device_request ON sync_operations(device_id, request_id);
