using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608220003_AuthorizationCore")]
public sealed class AuthorizationCore : Migration
{
    private static readonly Guid SystemAdminRoleId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid AuditorRoleId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "permissions", schema: DatabaseSchemas.System, columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            code = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
        }, constraints: table => table.PrimaryKey("pk_permissions", x => x.id));

        migrationBuilder.CreateTable(name: "roles", schema: DatabaseSchemas.System, columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
            created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            created_by = table.Column<Guid>(type: "uuid", nullable: true),
            updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            updated_by = table.Column<Guid>(type: "uuid", nullable: true),
            deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
            version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
        }, constraints: table => table.PrimaryKey("pk_roles", x => x.id));

        migrationBuilder.CreateTable(name: "user_scopes", schema: DatabaseSchemas.System, columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), user_id = table.Column<Guid>(type: "uuid", nullable: false),
            scope_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false), scope_id = table.Column<Guid>(type: "uuid", nullable: true),
            valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false), valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true), created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            created_by = table.Column<Guid>(type: "uuid", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("pk_user_scopes", x => x.id);
            table.ForeignKey("fk_user_scopes_users_user_id", x => x.user_id, principalSchema: DatabaseSchemas.System, principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable(name: "role_permissions", schema: DatabaseSchemas.System, columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), role_id = table.Column<Guid>(type: "uuid", nullable: false), permission_id = table.Column<Guid>(type: "uuid", nullable: false),
            granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false), granted_by = table.Column<Guid>(type: "uuid", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("pk_role_permissions", x => x.id);
            table.ForeignKey("fk_role_permissions_roles_role_id", x => x.role_id, principalSchema: DatabaseSchemas.System, principalTable: "roles", principalColumn: "id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("fk_role_permissions_permissions_permission_id", x => x.permission_id, principalSchema: DatabaseSchemas.System, principalTable: "permissions", principalColumn: "id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable(name: "user_roles", schema: DatabaseSchemas.System, columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), user_id = table.Column<Guid>(type: "uuid", nullable: false), role_id = table.Column<Guid>(type: "uuid", nullable: false),
            assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false), assigned_by = table.Column<Guid>(type: "uuid", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("pk_user_roles", x => x.id);
            table.ForeignKey("fk_user_roles_users_user_id", x => x.user_id, principalSchema: DatabaseSchemas.System, principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("fk_user_roles_roles_role_id", x => x.role_id, principalSchema: DatabaseSchemas.System, principalTable: "roles", principalColumn: "id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateIndex("ux_permissions_code", DatabaseSchemas.System, "permissions", "code", unique: true);
        migrationBuilder.CreateIndex("ux_roles_code", DatabaseSchemas.System, "roles", "code", unique: true);
        migrationBuilder.CreateIndex("ix_user_scopes_user_type_id", DatabaseSchemas.System, "user_scopes", ["user_id", "scope_type", "scope_id"]);
        migrationBuilder.CreateIndex("ix_user_scopes_user_id", DatabaseSchemas.System, "user_scopes", "user_id");
        migrationBuilder.CreateIndex("ix_role_permissions_permission_id", DatabaseSchemas.System, "role_permissions", "permission_id");
        migrationBuilder.CreateIndex("ux_role_permissions_role_permission", DatabaseSchemas.System, "role_permissions", ["role_id", "permission_id"], unique: true);
        migrationBuilder.CreateIndex("ix_user_roles_role_id", DatabaseSchemas.System, "user_roles", "role_id");
        migrationBuilder.CreateIndex("ux_user_roles_user_role", DatabaseSchemas.System, "user_roles", ["user_id", "role_id"], unique: true);
        SeedAuthorizationData(migrationBuilder);
    }

    private static void SeedAuthorizationData(MigrationBuilder migrationBuilder)
    {
        var seededAt = new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);
        migrationBuilder.InsertData(schema: DatabaseSchemas.System, table: "roles", columns: ["id", "code", "name", "description", "is_active", "created_at", "version"], values: new object[,]
        {
            { SystemAdminRoleId, "SYSTEM_ADMIN", "System Administrator", "Platform-level administration role.", true, seededAt, 1 },
            { AuditorRoleId, "AUDITOR", "Auditor", "Read-only audit role.", true, seededAt, 1 }
        });

        var permissionRows = new object[,]
        {
            { Guid.Parse("20000000-0000-0000-0000-000000000001"), "system.user.view", "View Users", "System", "View platform users.", true },
            { Guid.Parse("20000000-0000-0000-0000-000000000002"), "system.user.manage", "Manage Users", "System", "Create and manage platform users.", true },
            { Guid.Parse("20000000-0000-0000-0000-000000000003"), "system.role.view", "View Roles", "System", "View security roles.", true },
            { Guid.Parse("20000000-0000-0000-0000-000000000004"), "system.role.manage", "Manage Roles", "System", "Create roles and assign permissions.", true },
            { Guid.Parse("20000000-0000-0000-0000-000000000005"), "system.permission.view", "View Permissions", "System", "View the permission catalog.", true },
            { Guid.Parse("20000000-0000-0000-0000-000000000006"), "system.scope.view", "View Scopes", "System", "View access scopes.", true },
            { Guid.Parse("20000000-0000-0000-0000-000000000007"), "system.scope.manage", "Manage Scopes", "System", "Assign access scopes.", true },
            { Guid.Parse("20000000-0000-0000-0000-000000000008"), "audit.view", "View Audit", "Audit", "View audit records.", true }
        };
        migrationBuilder.InsertData(schema: DatabaseSchemas.System, table: "permissions", columns: ["id", "code", "name", "module", "description", "is_active"], values: permissionRows);

        var rolePermissionRows = new object[9, 5];
        for (var index = 0; index < 8; index++)
        {
            rolePermissionRows[index, 0] = Guid.Parse($"30000000-0000-0000-0000-{index + 1:000000000000}");
            rolePermissionRows[index, 1] = SystemAdminRoleId;
            rolePermissionRows[index, 2] = permissionRows[index, 0];
            rolePermissionRows[index, 3] = seededAt;
            rolePermissionRows[index, 4] = null!;
        }
        rolePermissionRows[8, 0] = Guid.Parse("30000000-0000-0000-0000-000000000009");
        rolePermissionRows[8, 1] = AuditorRoleId;
        rolePermissionRows[8, 2] = Guid.Parse("20000000-0000-0000-0000-000000000008");
        rolePermissionRows[8, 3] = seededAt;
        rolePermissionRows[8, 4] = null!;
        migrationBuilder.InsertData(schema: DatabaseSchemas.System, table: "role_permissions", columns: ["id", "role_id", "permission_id", "granted_at", "granted_by"], values: rolePermissionRows);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "role_permissions", schema: DatabaseSchemas.System);
        migrationBuilder.DropTable(name: "user_roles", schema: DatabaseSchemas.System);
        migrationBuilder.DropTable(name: "user_scopes", schema: DatabaseSchemas.System);
        migrationBuilder.DropTable(name: "permissions", schema: DatabaseSchemas.System);
        migrationBuilder.DropTable(name: "roles", schema: DatabaseSchemas.System);
    }
}
