-- Parte 5 del encargo post-Bloque-10: ampliar devices.type para aceptar WEB (push
-- también desde el dashboard Web, no solo Android/Wearable). El CHECK real
-- (devices_type_check) vive desde la migración 004, no la 003 — confirmado contra
-- Postgres real antes de escribir esta migración.
ALTER TABLE devices DROP CONSTRAINT devices_type_check;
ALTER TABLE devices ADD CONSTRAINT devices_type_check CHECK (type IN ('PHONE','WATCH','WEB'));
