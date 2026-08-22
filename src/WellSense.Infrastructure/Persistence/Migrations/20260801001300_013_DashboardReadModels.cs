using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 013 — corresponde 1:1 al bloque DDL "DashboardReadModels" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801001300_013_DashboardReadModels")]
    public partial class M013_DashboardReadModels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP MATERIALIZED VIEW IF EXISTS user_monthly_summary;
DROP MATERIALIZED VIEW IF EXISTS user_weekly_summary;
DROP MATERIALIZED VIEW IF EXISTS user_daily_summary;
            ");
        }
    }
}
