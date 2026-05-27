using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogImmutabilityTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Shared trigger function — raises an error on any mutation attempt.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_audit_log_immutable()
                RETURNS TRIGGER LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION
                        'audit_logs is append-only. UPDATE and DELETE operations are prohibited.';
                END;
                $$;
                """);

            // Prevent UPDATE
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_audit_log_prevent_update
                BEFORE UPDATE ON audit_logs
                FOR EACH ROW EXECUTE FUNCTION fn_audit_log_immutable();
                """);

            // Prevent DELETE
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_audit_log_prevent_delete
                BEFORE DELETE ON audit_logs
                FOR EACH ROW EXECUTE FUNCTION fn_audit_log_immutable();
                """);

            // 7-year retention policy: no automatic purging is configured.
            // Retention is enforced by operational policy per NFR-015 / DR-016.
            // Archive exports should be scheduled externally after year 7.
            migrationBuilder.Sql("""
                COMMENT ON TABLE audit_logs IS
                    'Append-only HIPAA audit log. Retention: 7 years (NFR-015).
                     UPDATE and DELETE are prevented by database triggers.
                     No automatic purging — managed by operational policy.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_audit_log_prevent_update ON audit_logs;");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_audit_log_prevent_delete ON audit_logs;");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS fn_audit_log_immutable();");

            migrationBuilder.Sql("COMMENT ON TABLE audit_logs IS NULL;");
        }
    }
}
