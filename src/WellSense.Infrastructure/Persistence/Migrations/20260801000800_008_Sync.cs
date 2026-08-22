using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 008 — corresponde 1:1 al bloque DDL "Sync" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801000800_008_Sync")]
    public partial class M008_Sync : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE sync_operations (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id         uuid NOT NULL REFERENCES devices(id),
    request_id        text NOT NULL,
    status            text NOT NULL DEFAULT 'PROCESSING'
                        CHECK (status IN ('PROCESSING','COMPLETED','FAILED')),
    accepted_count    integer NOT NULL DEFAULT 0,
    duplicated_count  integer NOT NULL DEFAULT 0,
    rejected_count    integer NOT NULL DEFAULT 0,
    created_at        timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_sync_operations_device_request ON sync_operations(device_id, request_id);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS sync_operations CASCADE;
            ");
        }
    }
}
