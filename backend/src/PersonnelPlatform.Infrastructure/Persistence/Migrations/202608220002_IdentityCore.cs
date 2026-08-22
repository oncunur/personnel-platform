using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608220002_IdentityCore")]
public sealed class IdentityCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            schema: DatabaseSchemas.System,
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                normalized_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                failed_login_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                security_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            schema: DatabaseSchemas.System,
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                security_version = table.Column<int>(type: "integer", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                device_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revoked_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                replaced_by_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_refresh_tokens", x => x.id);
                table.ForeignKey(
                    name: "fk_refresh_tokens_users_user_id",
                    column: x => x.user_id,
                    principalSchema: DatabaseSchemas.System,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_users_normalized_username",
            schema: DatabaseSchemas.System,
            table: "users",
            column: "normalized_username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_users_normalized_email",
            schema: DatabaseSchemas.System,
            table: "users",
            column: "normalized_email",
            unique: true,
            filter: "\"normalized_email\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_refresh_tokens_token_hash",
            schema: DatabaseSchemas.System,
            table: "refresh_tokens",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_user_expires",
            schema: DatabaseSchemas.System,
            table: "refresh_tokens",
            columns: ["user_id", "expires_at"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "refresh_tokens", schema: DatabaseSchemas.System);
        migrationBuilder.DropTable(name: "users", schema: DatabaseSchemas.System);
    }
}
