using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 001 — corresponde 1:1 al bloque DDL "InitUsers" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801000100_001_InitUsers")]
    public partial class M001_InitUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE users (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email           text NOT NULL,
    password_hash   text NOT NULL,
    password_algo   text NOT NULL DEFAULT 'argon2id',
    email_verified  boolean NOT NULL DEFAULT false,
    role            text NOT NULL DEFAULT 'user'
                      CHECK (role IN ('user','admin')),
    status          text NOT NULL DEFAULT 'active'
                      CHECK (status IN ('active','suspended','pending')),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    is_deleted      boolean NOT NULL DEFAULT false,
    deleted_at      timestamptz
);
CREATE UNIQUE INDEX ux_users_email_lower ON users (lower(email))
    WHERE is_deleted = false;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS users CASCADE;
            ");
        }
    }
}
