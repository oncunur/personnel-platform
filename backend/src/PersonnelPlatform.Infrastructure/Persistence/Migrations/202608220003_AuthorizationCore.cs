using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608220003_AuthorizationCore")]
public sealed class AuthorizationCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE system.permissions (
                id uuid NOT NULL CONSTRAINT pk_permissions PRIMARY KEY,
                code varchar(150) NOT NULL,
                name varchar(150) NOT NULL,
                module varchar(100) NOT NULL,
                description varchar(500) NULL,
                is_active boolean NOT NULL DEFAULT TRUE
            );
            CREATE UNIQUE INDEX ux_permissions_code ON system.permissions(code);

            CREATE TABLE system.roles (
                id uuid NOT NULL CONSTRAINT pk_roles PRIMARY KEY,
                code varchar(100) NOT NULL,
                name varchar(150) NOT NULL,
                description varchar(500) NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1
            );
            CREATE UNIQUE INDEX ux_roles_code ON system.roles(code);

            CREATE TABLE system.user_scopes (
                id uuid NOT NULL CONSTRAINT pk_user_scopes PRIMARY KEY,
                user_id uuid NOT NULL,
                scope_type varchar(30) NOT NULL,
                scope_id uuid NULL,
                valid_from timestamptz NOT NULL,
                valid_until timestamptz NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                CONSTRAINT fk_user_scopes_users_user_id FOREIGN KEY (user_id)
                    REFERENCES system.users(id) ON DELETE CASCADE
            );
            CREATE INDEX ix_user_scopes_user_type_id ON system.user_scopes(user_id, scope_type, scope_id);
            CREATE INDEX ix_user_scopes_user_id ON system.user_scopes(user_id);

            CREATE TABLE system.role_permissions (
                id uuid NOT NULL CONSTRAINT pk_role_permissions PRIMARY KEY,
                role_id uuid NOT NULL,
                permission_id uuid NOT NULL,
                granted_at timestamptz NOT NULL,
                granted_by uuid NULL,
                CONSTRAINT fk_role_permissions_roles_role_id FOREIGN KEY (role_id)
                    REFERENCES system.roles(id) ON DELETE CASCADE,
                CONSTRAINT fk_role_permissions_permissions_permission_id FOREIGN KEY (permission_id)
                    REFERENCES system.permissions(id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX ux_role_permissions_role_permission ON system.role_permissions(role_id, permission_id);
            CREATE INDEX ix_role_permissions_permission_id ON system.role_permissions(permission_id);

            CREATE TABLE system.user_roles (
                id uuid NOT NULL CONSTRAINT pk_user_roles PRIMARY KEY,
                user_id uuid NOT NULL,
                role_id uuid NOT NULL,
                assigned_at timestamptz NOT NULL,
                assigned_by uuid NULL,
                CONSTRAINT fk_user_roles_users_user_id FOREIGN KEY (user_id)
                    REFERENCES system.users(id) ON DELETE CASCADE,
                CONSTRAINT fk_user_roles_roles_role_id FOREIGN KEY (role_id)
                    REFERENCES system.roles(id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX ux_user_roles_user_role ON system.user_roles(user_id, role_id);
            CREATE INDEX ix_user_roles_role_id ON system.user_roles(role_id);

            INSERT INTO system.roles (id, code, name, description, is_active, created_at, version) VALUES
                ('10000000-0000-0000-0000-000000000001', 'SYSTEM_ADMIN', 'System Administrator', 'Platform-level administration role.', TRUE, '2026-08-22T00:00:00Z', 1),
                ('10000000-0000-0000-0000-000000000002', 'AUDITOR', 'Auditor', 'Read-only audit role.', TRUE, '2026-08-22T00:00:00Z', 1);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000001', 'system.user.view', 'View Users', 'System', 'View platform users.', TRUE),
                ('20000000-0000-0000-0000-000000000002', 'system.user.manage', 'Manage Users', 'System', 'Create and manage platform users.', TRUE),
                ('20000000-0000-0000-0000-000000000003', 'system.role.view', 'View Roles', 'System', 'View security roles.', TRUE),
                ('20000000-0000-0000-0000-000000000004', 'system.role.manage', 'Manage Roles', 'System', 'Create roles and assign permissions.', TRUE),
                ('20000000-0000-0000-0000-000000000005', 'system.permission.view', 'View Permissions', 'System', 'View the permission catalog.', TRUE),
                ('20000000-0000-0000-0000-000000000006', 'system.scope.view', 'View Scopes', 'System', 'View access scopes.', TRUE),
                ('20000000-0000-0000-0000-000000000007', 'system.scope.manage', 'Manage Scopes', 'System', 'Assign access scopes.', TRUE),
                ('20000000-0000-0000-0000-000000000008', 'audit.view', 'View Audit', 'Audit', 'View audit records.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', '2026-08-22T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002', '2026-08-22T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000003', '2026-08-22T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000004', '2026-08-22T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000005', '2026-08-22T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000006', '2026-08-22T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000007', '2026-08-22T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000008', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000008', '2026-08-22T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000009', '10000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000008', '2026-08-22T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS system.role_permissions;
            DROP TABLE IF EXISTS system.user_roles;
            DROP TABLE IF EXISTS system.user_scopes;
            DROP TABLE IF EXISTS system.permissions;
            DROP TABLE IF EXISTS system.roles;
            """);
    }
}
