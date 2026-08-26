using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 016 — fix urgente de producción. `audit_logs.ip_address` se creó como
    /// `inet` nativo desde la migración 002 (Bloque 1), pero `AuditLog.IpAddress` (C#)
    /// siempre fue un string plano sin conversión configurada en
    /// `AuditLogConfiguration` — nunca falló hasta que el Bloque 10 activó la auditoría
    /// completa (login, cambio de contraseña, etc.), momento en el que Npgsql empezó a
    /// enviar un parámetro `text` contra una columna `inet`
    /// (`42804: column "ip_address" is of type inet but expression is of type text`).
    ///
    /// Validado contra Postgres real: reproducido el error exacto de producción con un
    /// INSERT preparado (mismo mensaje, carácter por carácter), aplicada la migración,
    /// confirmado que el mismo INSERT ya funciona, revertida y vuelta a aplicar sin
    /// residuo.
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260826000000_016_AuditLogsIpAddressText")]
    public partial class M016_AuditLogsIpAddressText : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE audit_logs ALTER COLUMN ip_address TYPE text;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE audit_logs ALTER COLUMN ip_address TYPE inet USING ip_address::inet;");
        }
    }
}
