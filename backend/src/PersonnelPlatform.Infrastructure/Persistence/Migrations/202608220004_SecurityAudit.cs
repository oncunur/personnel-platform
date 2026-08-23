using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608220004_SecurityAudit")]
public sealed class SecurityAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "logs",
            schema: DatabaseSchemas.Audit,
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                succeeded = table.Column<bool>(type: "boolean", nullable: false),
                severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                metadata_json = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_logs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_audit_logs_occurred_at",
            schema: DatabaseSchemas.Audit,
            table: "logs",
            column: "occurred_at");

        migrationBuilder.CreateIndex(
            name: "ix_audit_logs_category_event_time",
            schema: DatabaseSchemas.Audit,
            table: "logs",
            columns: new[] { "category", "event_type", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_logs_actor_time",
            schema: DatabaseSchemas.Audit,
            table: "logs",
            columns: new[] { "actor_user_id", "occurred_at" });

        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION audit.prevent_audit_log_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'audit.logs is append-only';
            END;
            $$;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER trg_audit_logs_append_only
            BEFORE UPDATE OR DELETE ON audit.logs
            FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_log_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_logs_append_only ON audit.logs;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit.prevent_audit_log_mutation();");
        migrationBuilder.DropTable(name: "logs", schema: DatabaseSchemas.Audit);
    }
}
