using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 018 — Parte 5 (post-Bloque-10): amplía `devices.type` para aceptar
    /// 'WEB'. El CHECK real (`devices_type_check`) vive desde la migración 004 (no la
    /// 003, donde se ve `type text NOT NULL` sin restricción — esa es la definición de
    /// `profiles`/`goals`, no de `devices`). Validado contra Postgres real: confirmado
    /// que 'WEB' se rechazaba antes, aplicada la migración, confirmado que se acepta
    /// después, revertida (confirmado que vuelve a rechazarse) y vuelta a aplicar.
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260826020000_018_DeviceTypeWeb")]
    public partial class M018_DeviceTypeWeb : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE devices DROP CONSTRAINT devices_type_check;
ALTER TABLE devices ADD CONSTRAINT devices_type_check CHECK (type IN ('PHONE','WATCH','WEB'));
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE devices DROP CONSTRAINT devices_type_check;
ALTER TABLE devices ADD CONSTRAINT devices_type_check CHECK (type IN ('PHONE','WATCH'));
            ");
        }
    }
}
