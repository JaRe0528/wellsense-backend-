-- Bloque post-10 (fix urgente de producción): audit_logs.ip_address se creó como `inet`
-- nativo de Postgres desde la migración 002 (Bloque 1), pero AuditLog.IpAddress (C#) es
-- un string plano sin ninguna conversión configurada en AuditLogConfiguration — nunca
-- falló antes porque nada escribía en audit_logs hasta que el Bloque 10 activó la
-- auditoría completa (login, cambio de contraseña, etc.). No hace falta el tipo nativo
-- `inet` (no se usan operaciones de rango de IP en ningún lado) — se cambia a `text`
-- simple, coherente con lo que el modelo C# siempre esperó.
ALTER TABLE audit_logs ALTER COLUMN ip_address TYPE text;
