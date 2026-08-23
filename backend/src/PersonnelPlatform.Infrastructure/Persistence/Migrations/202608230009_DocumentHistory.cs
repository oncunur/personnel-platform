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

            CREATE OR REPLACE FUNCTION hr.capture_employee_document_history()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            DECLARE
                actor uuid;
                history_action varchar(40);
                history_reason varchar(1000);
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    actor := NEW.created_by;
                    IF actor IS NOT NULL THEN
                        history_action := CASE WHEN NEW.replaces_document_id IS NULL THEN 'UPLOADED' ELSE 'RENEWED' END;
                        INSERT INTO hr.employee_document_history
                            (id, employee_document_id, action, from_status, to_status, changed_by, changed_at, reason)
                        VALUES
                            (gen_random_uuid(), NEW.id, history_action, NULL, NEW.status, actor, NEW.created_at, NULL);
                    END IF;
                    RETURN NEW;
                END IF;

                IF OLD.status IS DISTINCT FROM NEW.status AND NEW.status IN ('ARCHIVED', 'CANCELLED') THEN
                    actor := COALESCE(NEW.updated_by, NEW.created_by);
                    IF actor IS NOT NULL THEN
                        history_action := CASE WHEN NEW.status = 'ARCHIVED' THEN 'ARCHIVED' ELSE 'CANCELLED' END;
                        history_reason := CASE WHEN NEW.status = 'ARCHIVED' THEN 'Belge yenileme nedeniyle arşivlendi.' ELSE NULL END;
                        INSERT INTO hr.employee_document_history
                            (id, employee_document_id, action, from_status, to_status, changed_by, changed_at, reason)
                        VALUES
                            (gen_random_uuid(), NEW.id, history_action, OLD.status, NEW.status, actor, COALESCE(NEW.updated_at, now()), history_reason);
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $function$;

            CREATE TRIGGER trg_employee_documents_history
            AFTER INSERT OR UPDATE OF status ON hr.employee_documents
            FOR EACH ROW EXECUTE FUNCTION hr.capture_employee_document_history();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_employee_documents_history ON hr.employee_documents;
            DROP FUNCTION IF EXISTS hr.capture_employee_document_history();
            DROP TABLE IF EXISTS hr.employee_document_history;
            """);
    }
}
