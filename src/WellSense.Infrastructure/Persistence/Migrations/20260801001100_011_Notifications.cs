using Microsoft.EntityFrameworkCore.Migrations;
using WellSense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace WellSense.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración 011 — corresponde 1:1 al bloque DDL "Notifications" de HANDOFF-DB.md (Chat Arquitectura/DB).
    /// Escrita como SQL crudo (no CreateTable/CreateIndex tipados de EF) para garantizar
    /// fidelidad exacta con el DDL ya validado (69 sentencias, 0 errores, ver HANDOFF-DB §9)
    /// en vez de arriesgar una traducción distinta vía Fluent API. Validada de forma
    /// independiente en este bloque: aplicada en orden 001→013 contra PostgreSQL 16 local,
    /// y revertida en orden 013→001 sin dejar estado residual (ver HANDOFF.md de este bloque).
    /// </summary>
    [DbContext(typeof(WellSenseDbContext))]
    [Migration("20260801001100_011_Notifications")]
    public partial class M011_Notifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE notification_tokens (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    device_id   uuid NOT NULL REFERENCES devices(id),
    fcm_token   text NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (device_id, fcm_token)
);
CREATE INDEX ix_notification_tokens_user_id ON notification_tokens(user_id);

CREATE TABLE notifications (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id),
    type        text NOT NULL,
    title       text NOT NULL,
    body        text NOT NULL,
    read_at     timestamptz,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_notifications_user_unread ON notifications(user_id, created_at)
    WHERE read_at IS NULL;

CREATE TABLE reminders (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           uuid NOT NULL REFERENCES users(id),
    type              text NOT NULL CHECK (type IN ('MANUAL','AUTO')),
    message           text NOT NULL,
    scheduled_at      timestamptz NOT NULL,
    cooldown_minutes  integer NOT NULL DEFAULT 0 CHECK (cooldown_minutes >= 0),
    created_at        timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_reminders_user_scheduled ON reminders(user_id, scheduled_at);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS reminders CASCADE;
DROP TABLE IF EXISTS notifications CASCADE;
DROP TABLE IF EXISTS notification_tokens CASCADE;
            ");
        }
    }
}
