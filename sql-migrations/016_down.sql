-- Reversión: vuelve a `inet`. Si para entonces ya existen filas con un valor de
-- ip_address que no sea una IP válida (no debería pasar — el código nunca escribió otra
-- cosa que direcciones IP reales o NULL), este ALTER fallaría; es responsabilidad de
-- quien revierte confirmar que los datos existentes son compatibles antes de correr esto
-- contra una base con datos reales.
ALTER TABLE audit_logs ALTER COLUMN ip_address TYPE inet USING ip_address::inet;
