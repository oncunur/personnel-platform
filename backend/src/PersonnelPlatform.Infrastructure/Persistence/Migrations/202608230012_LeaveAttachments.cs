using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230012_LeaveAttachments")]
public sealed class LeaveAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE hr.leave_attachments (
                id uuid NOT NULL CONSTRAINT pk_leave_attachments PRIMARY KEY,
                leave_id uuid NOT NULL,
                file_id uuid NOT NULL,
                description varchar(500) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_leave_attachments_leave FOREIGN KEY (leave_id) REFERENCES hr.leaves(id) ON DELETE CASCADE,
                CONSTRAINT fk_leave_attachments_file FOREIGN KEY (file_id) REFERENCES system.files(id) ON DELETE RESTRICT
            );
            CREATE INDEX ix_leave_attachments_leave ON hr.leave_attachments(leave_id);
            CREATE UNIQUE INDEX ux_leave_attachments_file ON hr.leave_attachments(file_id);

            CREATE OR REPLACE FUNCTION hr.validate_required_leave_attachment()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            DECLARE
                v_attachment_required boolean;
                v_has_attachment boolean;
            BEGIN
                IF OLD.status = 'DRAFT' AND NEW.status IN ('SUBMITTED', 'PENDING_APPROVAL') THEN
                    SELECT lt.attachment_required INTO v_attachment_required
                    FROM hr.leave_types lt
                    WHERE lt.id = NEW.leave_type_id AND lt.deleted_at IS NULL;

                    IF COALESCE(v_attachment_required, FALSE) THEN
                        SELECT EXISTS (
                            SELECT 1
                            FROM hr.leave_attachments la
                            JOIN system.files f ON f.id = la.file_id
                            WHERE la.leave_id = NEW.id
                              AND la.deleted_at IS NULL
                              AND f.status = 'ACTIVE'
                        ) INTO v_has_attachment;

                        IF NOT v_has_attachment THEN
                            RAISE EXCEPTION 'LEAVE_ATTACHMENT_REQUIRED' USING ERRCODE = 'P0001';
                        END IF;
                    END IF;
                END IF;
                RETURN NEW;
            END;
            $$;

            CREATE TRIGGER trg_00_validate_required_leave_attachment
            BEFORE UPDATE OF status ON hr.leaves
            FOR EACH ROW
            EXECUTE FUNCTION hr.validate_required_leave_attachment();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_00_validate_required_leave_attachment ON hr.leaves;
            DROP FUNCTION IF EXISTS hr.validate_required_leave_attachment();
            DROP TABLE IF EXISTS hr.leave_attachments;
            """);
    }
}
