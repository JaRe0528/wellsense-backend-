-- Parte 3+4 del encargo: límites reales por plan (no solo decorativos) + `features`
-- como arreglo de strings concretos (antes vacío '{}' para los 4 planes desde Bloque 1).
ALTER TABLE membership_plans ADD COLUMN limits jsonb NOT NULL DEFAULT '{}';

-- maxDevices/historyDays en null = sin límite (PROFESSIONAL). El resto de los valores
-- son decisión de este bloque, documentada en el HANDOFF — ajustables sin volver a
-- tocar código (viven en datos, no en una constante de C#).
UPDATE membership_plans SET
    limits = '{"maxDevices": 1, "historyDays": 7}',
    features = '["1 dispositivo vinculado", "7 días de historial de bienestar y estrés"]'
    WHERE code = 'FREE';

UPDATE membership_plans SET
    limits = '{"maxDevices": 2, "historyDays": 30}',
    features = '["Hasta 2 dispositivos vinculados", "30 días de historial de bienestar y estrés"]'
    WHERE code = 'BASIC';

UPDATE membership_plans SET
    limits = '{"maxDevices": 5, "historyDays": 90}',
    features = '["Hasta 5 dispositivos vinculados", "90 días de historial de bienestar y estrés"]'
    WHERE code = 'PRO';

UPDATE membership_plans SET
    limits = '{"maxDevices": null, "historyDays": null}',
    features = '["Dispositivos vinculados ilimitados", "Historial de bienestar y estrés ilimitado"]'
    WHERE code = 'PROFESSIONAL';
