using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 003 — corresponde 1:1 al bloque DDL "Profile" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801000300_003_Profile")]
    public partial class M003_Profile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE profiles (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL UNIQUE REFERENCES users(id),
    first_name  text,
    last_name   text,
    birth_date  date,
    weight_kg   numeric(5,2) CHECK (weight_kg > 0),
    height_cm   numeric(5,2) CHECK (height_cm > 0),
    occupation  text,
    avatar_url  text,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE goals (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id  uuid NOT NULL REFERENCES profiles(id),
    type        text NOT NULL,
    target_value numeric NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_goals_profile_id ON goals(profile_id);

CREATE TABLE onboarding_surveys (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    profile_id              uuid NOT NULL UNIQUE REFERENCES profiles(id),
    usual_schedule          text,
    sleep_schedule          text,
    declared_activity_level text,
    declared_stress_level   text NOT NULL
                              CHECK (declared_stress_level IN ('MUY_BAJO','BAJO','MODERADO','ALTO','MUY_ALTO')),
    declared_sleep_quality  text,
    created_at              timestamptz NOT NULL DEFAULT now()
);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS onboarding_surveys CASCADE;
DROP TABLE IF EXISTS goals CASCADE;
DROP TABLE IF EXISTS profiles CASCADE;
            ");
        }
    }
}
