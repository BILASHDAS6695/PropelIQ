using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_entity_type_entity_id",
                table: "audit_logs");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "audit_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "entity_type",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "details",
                table: "audit_logs",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}",
                oldClrType: typeof(JsonDocument),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "audit_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_id",
                table: "audit_logs",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_timestamp",
                table: "audit_logs",
                column: "timestamp");

            // PostgreSQL RULE: block UPDATE on audit_logs (append-only enforcement).
            // ADR-006 — second line of defence; AuditInterceptor is the first.
            // Retention: 7 years per NFR-015; no automated purge is configured.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE RULE audit_logs_no_update AS
                    ON UPDATE TO audit_logs
                    DO INSTEAD NOTHING;
            ");

            // PostgreSQL RULE: block DELETE on audit_logs.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE RULE audit_logs_no_delete AS
                    ON DELETE TO audit_logs
                    DO INSTEAD NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop immutability rules before reverting column/index changes.
            migrationBuilder.Sql("DROP RULE IF EXISTS audit_logs_no_update ON audit_logs;");
            migrationBuilder.Sql("DROP RULE IF EXISTS audit_logs_no_delete ON audit_logs;");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_entity_id",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_timestamp",
                table: "audit_logs");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "entity_type",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<JsonDocument>(
                name: "details",
                table: "audit_logs",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValue: "{}");

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });
        }
    }
}
