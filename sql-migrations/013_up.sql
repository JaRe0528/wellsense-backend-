CREATE MATERIALIZED VIEW user_daily_summary AS
SELECT
    user_id,
    date_trunc('day', recorded_at) AS day,
    now() AS refreshed_at
FROM measurements
GROUP BY user_id, date_trunc('day', recorded_at)
WITH NO DATA;

CREATE MATERIALIZED VIEW user_weekly_summary AS
SELECT
    user_id,
    date_trunc('week', recorded_at) AS week,
    now() AS refreshed_at
FROM measurements
GROUP BY user_id, date_trunc('week', recorded_at)
WITH NO DATA;

CREATE MATERIALIZED VIEW user_monthly_summary AS
SELECT
    user_id,
    date_trunc('month', recorded_at) AS month,
    now() AS refreshed_at
FROM measurements
GROUP BY user_id, date_trunc('month', recorded_at)
WITH NO DATA;

CREATE UNIQUE INDEX ux_user_daily_summary_user_day ON user_daily_summary(user_id, day);
CREATE UNIQUE INDEX ux_user_weekly_summary_user_week ON user_weekly_summary(user_id, week);
CREATE UNIQUE INDEX ux_user_monthly_summary_user_month ON user_monthly_summary(user_id, month);
