using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 004 — corresponde 1:1 al bloque DDL "Devices" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801000400_004_Devices")]
    public partial class M004_Devices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE devices (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    type        text NOT NULL CHECK (type IN ('PHONE','WATCH')),
    model       text,
    os_version  text,
    app_version text,
    last_seen_at timestamptz,
    status      text NOT NULL DEFAULT 'ACTIVE'
                  CHECK (status IN ('ACTIVE','INACTIVE','UNPAIRED')),
    paired_at   timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_devices_user_id ON devices(user_id);
CREATE INDEX ix_devices_user_type ON devices(user_id, type);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS devices CASCADE;
            ");
        }
    }
}
