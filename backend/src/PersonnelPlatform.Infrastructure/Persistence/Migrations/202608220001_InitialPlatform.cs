using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608220001_InitialPlatform")]
public sealed class InitialPlatform : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: DatabaseSchemas.System);
        migrationBuilder.EnsureSchema(name: DatabaseSchemas.Organization);
        migrationBuilder.EnsureSchema(name: DatabaseSchemas.Hr);
        migrationBuilder.EnsureSchema(name: DatabaseSchemas.Audit);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"DROP SCHEMA IF EXISTS {DatabaseSchemas.Audit} CASCADE;");
        migrationBuilder.Sql($"DROP SCHEMA IF EXISTS {DatabaseSchemas.Hr} CASCADE;");
        migrationBuilder.Sql($"DROP SCHEMA IF EXISTS {DatabaseSchemas.Organization} CASCADE;");
        migrationBuilder.Sql($"DROP SCHEMA IF EXISTS {DatabaseSchemas.System} CASCADE;");
    }
}
