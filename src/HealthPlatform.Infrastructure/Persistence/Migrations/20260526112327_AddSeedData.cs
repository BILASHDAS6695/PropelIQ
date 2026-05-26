using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HealthPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "insurance_records",
                columns: new[] { "id", "member_id", "provider_name", "status" },
                values: new object[,]
                {
                    { new Guid("22222222-0000-0000-0000-000000000001"), "MBR-000001", "BlueCross BlueShield", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000002"), "MBR-000002", "BlueCross BlueShield", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000003"), "MBR-000003", "BlueCross BlueShield", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000004"), "MBR-000004", "BlueCross BlueShield", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000005"), "MBR-000005", "BlueCross BlueShield", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000006"), "MBR-000006", "Aetna Health", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000007"), "MBR-000007", "Aetna Health", "Inactive" },
                    { new Guid("22222222-0000-0000-0000-000000000008"), "MBR-000008", "Aetna Health", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000009"), "MBR-000009", "Aetna Health", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000010"), "MBR-000010", "Aetna Health", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000011"), "MBR-000011", "United Healthcare", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000012"), "MBR-000012", "United Healthcare", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000013"), "MBR-000013", "United Healthcare", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000014"), "MBR-000014", "United Healthcare", "Inactive" },
                    { new Guid("22222222-0000-0000-0000-000000000015"), "MBR-000015", "United Healthcare", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000016"), "MBR-000016", "Cigna Medical", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000017"), "MBR-000017", "Cigna Medical", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000018"), "MBR-000018", "Cigna Medical", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000019"), "MBR-000019", "Cigna Medical", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000020"), "MBR-000020", "Cigna Medical", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000021"), "MBR-000021", "Humana Insurance", "Inactive" },
                    { new Guid("22222222-0000-0000-0000-000000000022"), "MBR-000022", "Humana Insurance", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000023"), "MBR-000023", "Humana Insurance", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000024"), "MBR-000024", "Humana Insurance", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000025"), "MBR-000025", "Humana Insurance", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000026"), "MBR-000026", "Anthem BCBS", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000027"), "MBR-000027", "Anthem BCBS", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000028"), "MBR-000028", "Anthem BCBS", "Inactive" },
                    { new Guid("22222222-0000-0000-0000-000000000029"), "MBR-000029", "Anthem BCBS", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000030"), "MBR-000030", "Anthem BCBS", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000031"), "MBR-000031", "Molina Healthcare", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000032"), "MBR-000032", "Molina Healthcare", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000033"), "MBR-000033", "Molina Healthcare", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000034"), "MBR-000034", "Molina Healthcare", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000035"), "MBR-000035", "Molina Healthcare", "Inactive" },
                    { new Guid("22222222-0000-0000-0000-000000000036"), "MBR-000036", "Centene Corporation", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000037"), "MBR-000037", "Centene Corporation", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000038"), "MBR-000038", "Centene Corporation", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000039"), "MBR-000039", "Centene Corporation", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000040"), "MBR-000040", "Centene Corporation", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000041"), "MBR-000041", "WellCare Health", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000042"), "MBR-000042", "WellCare Health", "Inactive" },
                    { new Guid("22222222-0000-0000-0000-000000000043"), "MBR-000043", "WellCare Health", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000044"), "MBR-000044", "WellCare Health", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000045"), "MBR-000045", "WellCare Health", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000046"), "MBR-000046", "Kaiser Permanente", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000047"), "MBR-000047", "Kaiser Permanente", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000048"), "MBR-000048", "Kaiser Permanente", "Active" },
                    { new Guid("22222222-0000-0000-0000-000000000049"), "MBR-000049", "Kaiser Permanente", "Inactive" },
                    { new Guid("22222222-0000-0000-0000-000000000050"), "MBR-000050", "Kaiser Permanente", "Active" }
                });

            migrationBuilder.InsertData(
                table: "providers",
                columns: new[] { "id", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "name", "schedule_template_id", "specialty", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "Dr. Sarah Mitchell", null, "Cardiology", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("11111111-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "Dr. James Okafor", null, "General Practice", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("11111111-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "Dr. Priya Sharma", null, "Neurology", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("11111111-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "Dr. Marcus Chen", null, "Orthopedics", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { new Guid("11111111-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, false, "Dr. Fatima Al-Rashid", null, "Pediatrics", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000029"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000030"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000031"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000032"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000033"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000034"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000035"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000036"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000037"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000038"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000039"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000040"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000041"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000042"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000043"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000044"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000045"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000046"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000047"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000048"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000049"));

            migrationBuilder.DeleteData(
                table: "insurance_records",
                keyColumn: "id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000050"));

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "providers",
                keyColumn: "id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000005"));
        }
    }
}
