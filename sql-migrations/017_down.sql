ALTER TABLE membership_plans DROP COLUMN limits;
UPDATE membership_plans SET features = '{}';
