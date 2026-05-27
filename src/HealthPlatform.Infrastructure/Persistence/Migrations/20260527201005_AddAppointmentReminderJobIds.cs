using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentReminderJobIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "mediated_by_user_id",
                table: "slot_swap_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "overridden_at",
                table: "slot_swap_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "override_reason",
                table: "slot_swap_requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "three_way_new_target_slot_id",
                table: "slot_swap_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "slot_swap_requests",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "reminder24h_job_id",
                table: "appointments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reminder2h_job_id",
                table: "appointments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mediated_by_user_id",
                table: "slot_swap_requests");

            migrationBuilder.DropColumn(
                name: "overridden_at",
                table: "slot_swap_requests");

            migrationBuilder.DropColumn(
                name: "override_reason",
                table: "slot_swap_requests");

            migrationBuilder.DropColumn(
                name: "three_way_new_target_slot_id",
                table: "slot_swap_requests");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "slot_swap_requests");

            migrationBuilder.DropColumn(
                name: "reminder24h_job_id",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "reminder2h_job_id",
                table: "appointments");
        }
    }
}
