using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230009_DocumentHistory")]
public sealed class DocumentHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE hr.employee_document_history (
                id uuid NOT NULL CONSTRAINT pk_employee_document_history PRIMARY KEY,
                employee_document_id uuid NOT NULL,
                action varchar(40) NOT NULL,
                from_status varchar(30) NULL,
                to_status varchar(30) NOT NULL,
                changed_by uuid NOT NULL,
                changed_at timestamptz NOT NULL,
                reason varchar(1000) NULL,
                metadata_json jsonb NULL,
                CONSTRAINT fk_employee_document_history_document FOREIGN KEY (employee_document_id) REFERENCES hr.employee_documents(id) ON DELETE CASCADE,
                CONSTRAINT fk_employee_document_history_user FOREIGN KEY (changed_by) REFERENCES system.users(id) ON DELETE RESTRICT
            );
            CREATE INDEX ix_employee_document_history_document_changed_at ON hr.employee_document_history(employee_document_id, changed_at DESC);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS hr.employee_document_history;");
    }
}
