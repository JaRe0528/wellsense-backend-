using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 009 — corresponde 1:1 al bloque DDL "MlScores" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801000900_009_MlScores")]
    public partial class M009_MlScores : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE wellness_scores (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    date        date NOT NULL,
    score       numeric NOT NULL CHECK (score BETWEEN 0 AND 100),
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, date)
);
CREATE INDEX ix_wellness_scores_user_date ON wellness_scores(user_id, date);

CREATE TABLE stress_scores (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    date        date NOT NULL,
    score       numeric NOT NULL CHECK (score BETWEEN 0 AND 100),
    level       text NOT NULL CHECK (level IN ('LOW','MEDIUM','HIGH')),
    confidence  numeric NOT NULL CHECK (confidence BETWEEN 0 AND 1),
    factors     jsonb NOT NULL DEFAULT '{}',
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, date)
);
CREATE INDEX ix_stress_scores_user_date ON stress_scores(user_id, date);

CREATE TABLE ml_predictions (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       uuid NOT NULL REFERENCES users(id),
    model_version text NOT NULL,
    type          text NOT NULL,
    input         jsonb NOT NULL,
    output        jsonb NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_ml_predictions_user_created ON ml_predictions(user_id, created_at);
CREATE INDEX ix_ml_predictions_model_version ON ml_predictions(model_version);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ml_predictions CASCADE;
DROP TABLE IF EXISTS stress_scores CASCADE;
DROP TABLE IF EXISTS wellness_scores CASCADE;
            ");
        }
    }
}
