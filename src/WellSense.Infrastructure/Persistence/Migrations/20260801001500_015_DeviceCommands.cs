using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 015 — Bloque 8 (Device Command System). No estaba en el DDL original
    /// de HANDOFF-DB — extensión de esquema propuesta por este bloque, mismo criterio
    /// que la migración 014 (timezone, Bloque 3). Validada de la misma forma que las
    /// anteriores: aplicada, ciclo de vida completo probado con UPDATE reales
    /// (PENDING→DELIVERED→ACKNOWLEDGED, ambos CHECK de consistencia confirmados
    /// rechazando transiciones inválidas), revertida sin residuo, y vuelta a aplicar.
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801001500_015_DeviceCommands")]
    public partial class M015_DeviceCommands : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE device_commands (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id        uuid NOT NULL REFERENCES devices(id),
    user_id          uuid NOT NULL REFERENCES users(id),
    type             text NOT NULL
                       CHECK (type IN ('START_MONITORING','STOP_MONITORING','CHANGE_INTERVAL','SYNC_NOW','REQUEST_STATUS')),
    payload          jsonb NOT NULL DEFAULT '{}',
    status           text NOT NULL DEFAULT 'PENDING'
                       CHECK (status IN ('PENDING','DELIVERED','ACKNOWLEDGED','FAILED','EXPIRED')),
    ack_payload      jsonb,
    created_at       timestamptz NOT NULL DEFAULT now(),
    delivered_at     timestamptz,
    acknowledged_at  timestamptz,
    expires_at       timestamptz NOT NULL DEFAULT (now() + interval '24 hours'),
    CHECK (status NOT IN ('DELIVERED','ACKNOWLEDGED','FAILED') OR delivered_at IS NOT NULL),
    CHECK ((status IN ('ACKNOWLEDGED','FAILED')) = (acknowledged_at IS NOT NULL))
);
CREATE INDEX ix_device_commands_device_status ON device_commands(device_id, status);
CREATE INDEX ix_device_commands_user_created ON device_commands(user_id, created_at);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS device_commands CASCADE;
            ");
        }
    }
}
