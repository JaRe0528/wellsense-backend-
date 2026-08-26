-- Reversión: si para entonces ya existen filas con type='WEB', este ALTER fallaría (el
-- CHECK no se podría satisfacer) — responsabilidad de quien revierte confirmar que no
-- hay datos WEB antes de correr esto contra una base con datos reales.
ALTER TABLE devices DROP CONSTRAINT devices_type_check;
ALTER TABLE devices ADD CONSTRAINT devices_type_check CHECK (type IN ('PHONE','WATCH'));
