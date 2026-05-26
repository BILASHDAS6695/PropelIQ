using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "providers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "providers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "providers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "patient_views360",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "patient_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "patient_profiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "patient_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "intake_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "intake_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "intake_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "clinical_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "clinical_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "clinical_documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "appointments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "appointments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "patient_views360");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "patient_profiles");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "intake_records");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "intake_records");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "intake_records");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "clinical_documents");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "appointments");
        }
    }
}
