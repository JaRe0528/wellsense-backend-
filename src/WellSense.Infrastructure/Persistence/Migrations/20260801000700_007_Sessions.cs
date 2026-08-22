using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 007 — corresponde 1:1 al bloque DDL "Sessions" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801000700_007_Sessions")]
    public partial class M007_Sessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE sleep_sessions (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         uuid NOT NULL REFERENCES users(id),
    start_at        timestamptz NOT NULL,
    end_at          timestamptz NOT NULL CHECK (end_at > start_at),
    duration_minutes integer GENERATED ALWAYS AS
                      (round(extract(epoch FROM (end_at - start_at)) / 60)::integer) STORED,
    stages          jsonb NOT NULL DEFAULT '{}',
    created_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_sleep_sessions_user_start ON sleep_sessions(user_id, start_at);

CREATE TABLE activity_sessions (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    type        text NOT NULL,
    start_at    timestamptz NOT NULL,
    end_at      timestamptz NOT NULL CHECK (end_at > start_at),
    steps       integer CHECK (steps >= 0),
    distance_m  numeric CHECK (distance_m >= 0),
    calories    numeric CHECK (calories >= 0),
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_activity_sessions_user_start ON activity_sessions(user_id, start_at);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS activity_sessions CASCADE;
DROP TABLE IF EXISTS sleep_sessions CASCADE;
            ");
        }
    }
}
