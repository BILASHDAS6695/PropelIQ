using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentBookingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "visit_reason",
                table: "appointments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "appointment_slots",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_id_provider_id_slot_time",
                table: "appointments",
                columns: new[] { "patient_id", "provider_id", "slot_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_patient_id_provider_id_slot_time",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "visit_reason",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "appointment_slots");
        }
    }
}
