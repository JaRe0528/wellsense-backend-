using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 017 — límites reales por plan (Parte 3) + `features` como arreglo de
    /// strings concretos, no vacío (Parte 4). Validada contra Postgres real: aplicada,
    /// confirmados los 4 planes con sus límites/features correctos, revertida sin
    /// residuo, vuelta a aplicar.
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260826010000_017_MembershipPlanLimits")]
    public partial class M017_MembershipPlanLimits : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE membership_plans ADD COLUMN limits jsonb NOT NULL DEFAULT '{}';

UPDATE membership_plans SET
    limits = '{""maxDevices"": 1, ""historyDays"": 7}',
    features = '[""1 dispositivo vinculado"", ""7 días de historial de bienestar y estrés""]'
    WHERE code = 'FREE';

UPDATE membership_plans SET
    limits = '{""maxDevices"": 2, ""historyDays"": 30}',
    features = '[""Hasta 2 dispositivos vinculados"", ""30 días de historial de bienestar y estrés""]'
    WHERE code = 'BASIC';

UPDATE membership_plans SET
    limits = '{""maxDevices"": 5, ""historyDays"": 90}',
    features = '[""Hasta 5 dispositivos vinculados"", ""90 días de historial de bienestar y estrés""]'
    WHERE code = 'PRO';

UPDATE membership_plans SET
    limits = '{""maxDevices"": null, ""historyDays"": null}',
    features = '[""Dispositivos vinculados ilimitados"", ""Historial de bienestar y estrés ilimitado""]'
    WHERE code = 'PROFESSIONAL';
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE membership_plans DROP COLUMN limits;
UPDATE membership_plans SET features = '{}';
            ");
        }
    }
}
