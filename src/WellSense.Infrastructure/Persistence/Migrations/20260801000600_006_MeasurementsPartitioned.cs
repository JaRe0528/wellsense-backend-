using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 006 — corresponde 1:1 al bloque DDL "MeasurementsPartitioned" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801000600_006_MeasurementsPartitioned")]
    public partial class M006_MeasurementsPartitioned : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE measurements (
    id          uuid NOT NULL,
    user_id     uuid NOT NULL REFERENCES users(id),
    device_id   uuid NOT NULL REFERENCES devices(id),
    type        text NOT NULL
                  CHECK (type IN ('HEART_RATE','STEPS','SPO2','CALORIES','SKIN_TEMP')),
    value       numeric NOT NULL,
    unit        text NOT NULL,
    recorded_at timestamptz NOT NULL,
    synced_at   timestamptz,
    created_at  timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id, recorded_at)
) PARTITION BY RANGE (recorded_at);

CREATE UNIQUE INDEX ux_measurements_device_event ON measurements(device_id, id, recorded_at);
CREATE INDEX ix_measurements_user_recorded ON measurements(user_id, recorded_at);

-- partición inicial (mes de despliegue) — el resto las crea el job de pg_partman (Chat DevSecOps)
CREATE TABLE measurements_2026_08 PARTITION OF measurements
    FOR VALUES FROM ('2026-08-01') TO ('2026-09-01');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS measurements_2026_08;
DROP TABLE IF EXISTS measurements CASCADE;
            ");
        }
    }
}
