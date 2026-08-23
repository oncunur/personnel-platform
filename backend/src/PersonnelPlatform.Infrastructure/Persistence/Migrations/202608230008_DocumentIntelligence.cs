using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230008_DocumentIntelligence")]
public sealed class DocumentIntelligence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000039', 'documents.expiring.view', 'View Expiring Documents', 'Documents', 'View expiring and expired personnel documents in authorized scope.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000039', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000039', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id = '30000000-0000-0000-0000-000000000039';
            DELETE FROM system.permissions WHERE id = '20000000-0000-0000-0000-000000000039';
            """);
    }
}
