using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230007_DocumentsCore")]
public sealed class DocumentsCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE system.files (
                id uuid NOT NULL CONSTRAINT pk_files PRIMARY KEY,
                original_name varchar(255) NOT NULL,
                storage_key varchar(500) NOT NULL,
                content_type varchar(150) NOT NULL,
                extension varchar(20) NOT NULL,
                size_bytes bigint NOT NULL,
                sha256 varchar(64) NOT NULL,
                provider varchar(50) NOT NULL,
                status varchar(30) NOT NULL,
                uploaded_by uuid NOT NULL,
                uploaded_at timestamptz NOT NULL,
                CONSTRAINT fk_files_users_uploaded_by FOREIGN KEY (uploaded_by) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_files_size CHECK (size_bytes > 0),
                CONSTRAINT ck_files_status CHECK (status IN ('PENDING','ACTIVE','FAILED','QUARANTINED','DELETED'))
            );
            CREATE UNIQUE INDEX ux_files_storage_key ON system.files(storage_key);
            CREATE INDEX ix_files_sha256 ON system.files(sha256);
            CREATE INDEX ix_files_status ON system.files(status);

            CREATE TABLE hr.document_types (
                id uuid NOT NULL CONSTRAINT pk_document_types PRIMARY KEY,
                code varchar(80) NOT NULL,
                name varchar(150) NOT NULL,
                description varchar(1000) NULL,
                required_by_default boolean NOT NULL DEFAULT FALSE,
                expiration_required boolean NOT NULL DEFAULT FALSE,
                default_validity_days integer NULL,
                file_required boolean NOT NULL DEFAULT TRUE,
                document_number_required boolean NOT NULL DEFAULT FALSE,
                multiple_allowed boolean NOT NULL DEFAULT FALSE,
                reminder_days_csv varchar(200) NOT NULL DEFAULT '90,60,30,15,7,1,0',
                is_active boolean NOT NULL DEFAULT TRUE,
                display_order integer NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT ck_document_types_default_validity CHECK (default_validity_days IS NULL OR default_validity_days > 0)
            );
            CREATE UNIQUE INDEX ux_document_types_code ON hr.document_types(code);

            CREATE TABLE hr.document_type_employee_types (
                id uuid NOT NULL CONSTRAINT pk_document_type_employee_types PRIMARY KEY,
                document_type_id uuid NOT NULL,
                employee_type_id uuid NOT NULL,
                is_required boolean NOT NULL DEFAULT TRUE,
                valid_from date NULL,
                valid_until date NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NOT NULL,
                CONSTRAINT fk_document_type_employee_types_document_types_document_type_id FOREIGN KEY (document_type_id) REFERENCES hr.document_types(id) ON DELETE CASCADE,
                CONSTRAINT fk_document_type_employee_types_employee_types_employee_type_id FOREIGN KEY (employee_type_id) REFERENCES hr.employee_types(id) ON DELETE RESTRICT,
                CONSTRAINT ck_document_type_employee_types_dates CHECK (valid_until IS NULL OR valid_from IS NULL OR valid_until >= valid_from)
            );
            CREATE UNIQUE INDEX ux_document_type_employee_types_type_employee ON hr.document_type_employee_types(document_type_id, employee_type_id);
            CREATE INDEX ix_document_type_employee_types_employee_type ON hr.document_type_employee_types(employee_type_id);

            CREATE TABLE hr.employee_documents (
                id uuid NOT NULL CONSTRAINT pk_employee_documents PRIMARY KEY,
                employee_id uuid NOT NULL,
                document_type_id uuid NOT NULL,
                file_id uuid NULL,
                document_number varchar(150) NULL,
                issue_date date NULL,
                valid_from date NULL,
                valid_until date NULL,
                issuing_authority varchar(200) NULL,
                country_code varchar(3) NULL,
                status varchar(30) NOT NULL DEFAULT 'VALID',
                notes varchar(2000) NULL,
                replaces_document_id uuid NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_employee_documents_employees_employee_id FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_documents_document_types_document_type_id FOREIGN KEY (document_type_id) REFERENCES hr.document_types(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_documents_files_file_id FOREIGN KEY (file_id) REFERENCES system.files(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_documents_employee_documents_replaces_document_id FOREIGN KEY (replaces_document_id) REFERENCES hr.employee_documents(id) ON DELETE RESTRICT,
                CONSTRAINT ck_employee_documents_status CHECK (status IN ('VALID','EXPIRING','EXPIRED','ARCHIVED','CANCELLED')),
                CONSTRAINT ck_employee_documents_dates CHECK (valid_until IS NULL OR valid_from IS NULL OR valid_until >= valid_from),
                CONSTRAINT ck_employee_documents_issue_expiry CHECK (valid_until IS NULL OR issue_date IS NULL OR valid_until >= issue_date)
            );
            CREATE INDEX ix_employee_documents_employee_type ON hr.employee_documents(employee_id, document_type_id);
            CREATE INDEX ix_employee_documents_status_valid_until ON hr.employee_documents(status, valid_until);
            CREATE INDEX ix_employee_documents_file_id ON hr.employee_documents(file_id);
            CREATE INDEX ix_employee_documents_replaces_document_id ON hr.employee_documents(replaces_document_id);

            INSERT INTO hr.document_types (id, code, name, description, required_by_default, expiration_required, default_validity_days, file_required, document_number_required, multiple_allowed, reminder_days_csv, is_active, display_order, created_at, version) VALUES
                ('41000000-0000-0000-0000-000000000001', 'IDENTITY_CARD', 'Kimlik Belgesi', 'Genel kimlik belgesi türü.', FALSE, TRUE, NULL, TRUE, TRUE, FALSE, '90,60,30,15,7,1,0', TRUE, 10, '2026-08-23T00:00:00Z', 1),
                ('41000000-0000-0000-0000-000000000002', 'PASSPORT', 'Pasaport', 'Pasaport belgesi.', FALSE, TRUE, NULL, TRUE, TRUE, FALSE, '180,90,60,30,15,7,1,0', TRUE, 20, '2026-08-23T00:00:00Z', 1),
                ('41000000-0000-0000-0000-000000000003', 'EMPLOYMENT_CONTRACT', 'İş Sözleşmesi', 'Personel sözleşme belgesi.', FALSE, FALSE, NULL, TRUE, FALSE, TRUE, '', TRUE, 30, '2026-08-23T00:00:00Z', 1),
                ('41000000-0000-0000-0000-000000000004', 'MEDICAL_REPORT', 'Sağlık Raporu', 'Sağlık/işe uygunluk belgesi.', FALSE, TRUE, NULL, TRUE, FALSE, TRUE, '90,60,30,15,7,1,0', TRUE, 40, '2026-08-23T00:00:00Z', 1),
                ('41000000-0000-0000-0000-000000000005', 'WORK_PERMIT', 'Çalışma İzni', 'Çalışma izni belgesi.', FALSE, TRUE, NULL, TRUE, TRUE, TRUE, '180,90,60,30,15,7,1,0', TRUE, 50, '2026-08-23T00:00:00Z', 1);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000031', 'documents.type.view', 'View Document Types', 'Documents', 'View personnel document types.', TRUE),
                ('20000000-0000-0000-0000-000000000032', 'documents.type.manage', 'Manage Document Types', 'Documents', 'Create and manage document types and requirements.', TRUE),
                ('20000000-0000-0000-0000-000000000033', 'documents.employee.view', 'View Employee Documents', 'Documents', 'View personnel document metadata.', TRUE),
                ('20000000-0000-0000-0000-000000000034', 'documents.employee.upload', 'Upload Employee Documents', 'Documents', 'Upload personnel documents.', TRUE),
                ('20000000-0000-0000-0000-000000000035', 'documents.employee.renew', 'Renew Employee Documents', 'Documents', 'Renew personnel documents without overwriting history.', TRUE),
                ('20000000-0000-0000-0000-000000000036', 'documents.employee.cancel', 'Cancel Employee Documents', 'Documents', 'Cancel personnel document records.', TRUE),
                ('20000000-0000-0000-0000-000000000037', 'documents.file.view', 'View Document Files', 'Documents', 'Open protected personnel document files.', TRUE),
                ('20000000-0000-0000-0000-000000000038', 'documents.missing.view', 'View Missing Documents', 'Documents', 'View missing mandatory personnel documents.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000031', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000031', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000032', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000032', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000033', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000033', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000034', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000034', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000035', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000035', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000036', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000036', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000037', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000037', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000038', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000038', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000031','30000000-0000-0000-0000-000000000032','30000000-0000-0000-0000-000000000033','30000000-0000-0000-0000-000000000034',
                '30000000-0000-0000-0000-000000000035','30000000-0000-0000-0000-000000000036','30000000-0000-0000-0000-000000000037','30000000-0000-0000-0000-000000000038');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000031','20000000-0000-0000-0000-000000000032','20000000-0000-0000-0000-000000000033','20000000-0000-0000-0000-000000000034',
                '20000000-0000-0000-0000-000000000035','20000000-0000-0000-0000-000000000036','20000000-0000-0000-0000-000000000037','20000000-0000-0000-0000-000000000038');
            DROP TABLE IF EXISTS hr.employee_documents;
            DROP TABLE IF EXISTS hr.document_type_employee_types;
            DROP TABLE IF EXISTS hr.document_types;
            DROP TABLE IF EXISTS system.files;
            """);
    }
}
