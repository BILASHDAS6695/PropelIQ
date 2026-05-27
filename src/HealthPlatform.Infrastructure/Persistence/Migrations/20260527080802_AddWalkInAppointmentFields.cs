using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalkInAppointmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_slot_id",
                table: "appointments");

            migrationBuilder.AlterColumn<Guid>(
                name: "slot_id",
                table: "appointments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "arrival_time",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "queue_position",
                table: "appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_provider_id_arrival_time",
                table: "appointments",
                columns: new[] { "provider_id", "arrival_time" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_slot_id",
                table: "appointments",
                column: "slot_id",
                unique: true,
                filter: "slot_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_provider_id_arrival_time",
                table: "appointments");

            migrationBuilder.DropIndex(
                name: "ix_appointments_slot_id",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "arrival_time",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "queue_position",
                table: "appointments");

            migrationBuilder.AlterColumn<Guid>(
                name: "slot_id",
                table: "appointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_slot_id",
                table: "appointments",
                column: "slot_id",
                unique: true);
        }
    }
}
