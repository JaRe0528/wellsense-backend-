-- Bloque 3 (Users + Profile): soporte para la decisión de zona horaria de
-- wellness_scores/stress_scores — ver HANDOFF de Bloque 3 para la justificación
-- completa. 'UTC' como default es un fallback seguro y universal para perfiles
-- que todavía no la configuraron explícitamente (no rompe nada, solo alinea sus
-- "días" a UTC hasta que el cliente envíe su zona real).
ALTER TABLE profiles
    ADD COLUMN timezone text NOT NULL DEFAULT 'UTC';
