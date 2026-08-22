using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 005 — corresponde 1:1 al bloque DDL "DeviceLinkCodes" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801000500_005_DeviceLinkCodes")]
    public partial class M005_DeviceLinkCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE device_link_codes (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       uuid NOT NULL REFERENCES users(id),
    code_hash     text NOT NULL,
    attempts      integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    max_attempts  integer NOT NULL DEFAULT 5 CHECK (max_attempts > 0),
    expires_at    timestamptz NOT NULL,
    used_at       timestamptz,
    device_id     uuid REFERENCES devices(id),
    created_at    timestamptz NOT NULL DEFAULT now(),
    CHECK (attempts <= max_attempts),
    CHECK (expires_at > created_at),
    CHECK ((used_at IS NULL) = (device_id IS NULL))
);

CREATE UNIQUE INDEX ux_device_link_codes_one_active_per_user
    ON device_link_codes(user_id) WHERE used_at IS NULL;

CREATE UNIQUE INDEX ux_device_link_codes_active_code_hash
    ON device_link_codes(code_hash) WHERE used_at IS NULL;

CREATE INDEX ix_device_link_codes_expires_at
    ON device_link_codes(expires_at) WHERE used_at IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS device_link_codes CASCADE;
            ");
        }
    }
}
