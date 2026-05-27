using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderScheduleEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "appointment_slots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Available");

            // Migrate existing blocked (is_available = false) slots before dropping the column.
            migrationBuilder.Sql(
                "UPDATE appointment_slots SET status = 'Blocked' WHERE is_available = false;");

            migrationBuilder.DropColumn(
                name: "is_available",
                table: "appointment_slots");

            migrationBuilder.CreateTable(
                name: "provider_schedule_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    slot_duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_schedule_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_provider_schedule_rules_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_unavailabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unavailable_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_unavailabilities", x => x.id);
                    table.ForeignKey(
                        name: "fk_provider_unavailabilities_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_slots_status",
                table: "appointment_slots",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_provider_schedule_rules_provider_id_day_of_week",
                table: "provider_schedule_rules",
                columns: new[] { "provider_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_unavailabilities_provider_id_unavailable_date",
                table: "provider_unavailabilities",
                columns: new[] { "provider_id", "unavailable_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_schedule_rules");

            migrationBuilder.DropTable(
                name: "provider_unavailabilities");

            migrationBuilder.DropIndex(
                name: "ix_appointment_slots_status",
                table: "appointment_slots");

            migrationBuilder.DropColumn(
                name: "status",
                table: "appointment_slots");

            migrationBuilder.AddColumn<bool>(
                name: "is_available",
                table: "appointment_slots",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
