using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 014 — Bloque 3 (Users + Profile). Agrega `profiles.timezone`, requerida
    /// por la decisión de zona horaria de wellness_scores/stress_scores (ver HANDOFF de
    /// Bloque 3: se computa el "día" en la zona LOCAL del usuario, no en UTC). No estaba
    /// en el DDL original de HANDOFF-DB (001-013) — es una extensión de esquema propuesta
    /// por este bloque, que el chat de DB/orquestador debería confirmar si en algún
    /// momento se vuelve a tocar el diseño maestro de la tabla `profiles`.
    /// Validada de la misma forma que 001-013: aplicada sobre 001-013 ya migradas,
    /// verificado el default 'UTC' en una fila real, y revertida sin dejar residuo.
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801001400_014_ProfileTimezone")]
    public partial class M014_ProfileTimezone : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE profiles
    ADD COLUMN timezone text NOT NULL DEFAULT 'UTC';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE profiles DROP COLUMN IF EXISTS timezone;
            ");
        }
    }
}
