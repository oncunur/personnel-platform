using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230027_SecurityHardening")]
public sealed class SecurityHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE system.refresh_tokens
                ADD COLUMN IF NOT EXISTS mfa_verified boolean NOT NULL DEFAULT FALSE;

            CREATE TABLE system.user_mfa_credentials (
                id uuid NOT NULL CONSTRAINT pk_user_mfa_credentials PRIMARY KEY,
                user_id uuid NOT NULL,
                protected_secret varchar(2000) NOT NULL,
                is_enabled boolean NOT NULL DEFAULT FALSE,
                enabled_at timestamptz NULL,
                last_accepted_time_step bigint NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_user_mfa_credential_user FOREIGN KEY (user_id) REFERENCES system.users(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_user_mfa_credential_user ON system.user_mfa_credentials(user_id) WHERE deleted_at IS NULL;

            CREATE TABLE system.mfa_challenges (
                id uuid NOT NULL CONSTRAINT pk_mfa_challenges PRIMARY KEY,
                user_id uuid NOT NULL,
                token_hash varchar(128) NOT NULL,
                purpose varchar(30) NOT NULL,
                expires_at timestamptz NOT NULL,
                created_at timestamptz NOT NULL,
                consumed_at timestamptz NULL,
                failed_attempt_count integer NOT NULL DEFAULT 0,
                ip_address varchar(100) NULL,
                device_name varchar(200) NULL,
                CONSTRAINT fk_mfa_challenge_user FOREIGN KEY (user_id) REFERENCES system.users(id) ON DELETE RESTRICT,
                CONSTRAINT ck_mfa_challenge_purpose CHECK (purpose IN ('LOGIN','ENROLLMENT')),
                CONSTRAINT ck_mfa_challenge_failed_attempts CHECK (failed_attempt_count >= 0 AND failed_attempt_count <= 5),
                CONSTRAINT ck_mfa_challenge_expiry CHECK (expires_at > created_at)
            );
            CREATE UNIQUE INDEX ux_mfa_challenge_token_hash ON system.mfa_challenges(token_hash);
            CREATE INDEX ix_mfa_challenge_user_expiry ON system.mfa_challenges(user_id, expires_at DESC);

            CREATE TABLE hr.employee_sensitive_profiles (
                id uuid NOT NULL CONSTRAINT pk_employee_sensitive_profiles PRIMARY KEY,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                national_id_ciphertext varchar(2000) NULL,
                iban_ciphertext varchar(2000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_employee_sensitive_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_sensitive_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT ck_employee_sensitive_has_value CHECK (national_id_ciphertext IS NOT NULL OR iban_ciphertext IS NOT NULL)
            );
            CREATE UNIQUE INDEX ux_employee_sensitive_employee ON hr.employee_sensitive_profiles(employee_id) WHERE deleted_at IS NULL;
            CREATE INDEX ix_employee_sensitive_company ON hr.employee_sensitive_profiles(company_id);

            CREATE TABLE payroll.compensation_salary_secrets (
                id uuid NOT NULL CONSTRAINT pk_compensation_salary_secrets PRIMARY KEY,
                compensation_id uuid NOT NULL,
                company_id uuid NOT NULL,
                employee_id uuid NOT NULL,
                protected_monthly_base_salary varchar(2000) NOT NULL,
                key_version integer NOT NULL DEFAULT 1,
                created_at timestamptz NOT NULL,
                CONSTRAINT fk_compensation_salary_secret_compensation FOREIGN KEY (compensation_id) REFERENCES payroll.employee_compensations(id) ON DELETE RESTRICT,
                CONSTRAINT fk_compensation_salary_secret_company FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_compensation_salary_secret_employee FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT ck_compensation_salary_secret_key_version CHECK (key_version > 0)
            );
            CREATE UNIQUE INDEX ux_compensation_salary_secret_compensation ON payroll.compensation_salary_secrets(compensation_id);
            CREATE INDEX ix_compensation_salary_secret_company_employee ON payroll.compensation_salary_secrets(company_id, employee_id);

            CREATE OR REPLACE FUNCTION payroll.clear_plaintext_salary_after_secret()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                UPDATE payroll.employee_compensations
                   SET monthly_base_salary = 0
                 WHERE id = NEW.compensation_id
                   AND monthly_base_salary <> 0;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_compensation_salary_secret_clear_plaintext
                AFTER INSERT ON payroll.compensation_salary_secrets
                FOR EACH ROW EXECUTE FUNCTION payroll.clear_plaintext_salary_after_secret();

            CREATE OR REPLACE FUNCTION payroll.prevent_plaintext_salary_restore()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.monthly_base_salary <> 0
                   AND EXISTS (SELECT 1 FROM payroll.compensation_salary_secrets s WHERE s.compensation_id = NEW.id) THEN
                    RAISE EXCEPTION 'PAYROLL_PLAINTEXT_SALARY_FORBIDDEN' USING ERRCODE = 'P0001';
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER trg_compensation_salary_plaintext_guard
                BEFORE UPDATE OF monthly_base_salary ON payroll.employee_compensations
                FOR EACH ROW EXECUTE FUNCTION payroll.prevent_plaintext_salary_restore();

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000136', 'personnel.sensitive.view', 'View Sensitive Personnel Metadata', 'Personnel', 'View masked identity and IBAN metadata.', TRUE),
                ('20000000-0000-0000-0000-000000000137', 'personnel.sensitive.reveal', 'Reveal Sensitive Personnel Data', 'Personnel', 'Reveal decrypted identity and IBAN values.', TRUE),
                ('20000000-0000-0000-0000-000000000138', 'personnel.sensitive.manage', 'Manage Sensitive Personnel Data', 'Personnel', 'Create and update encrypted identity and IBAN values.', TRUE),
                ('20000000-0000-0000-0000-000000000139', 'payroll.salary.reveal', 'Reveal Salary Amounts', 'Payroll', 'Reveal decrypted employee compensation and payroll amounts.', TRUE)
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000136', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000136', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000137', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000137', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000138', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000138', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000139', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000139', '2026-08-23T00:00:00Z')
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000136','30000000-0000-0000-0000-000000000137',
                '30000000-0000-0000-0000-000000000138','30000000-0000-0000-0000-000000000139');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000136','20000000-0000-0000-0000-000000000137',
                '20000000-0000-0000-0000-000000000138','20000000-0000-0000-0000-000000000139');

            DROP TRIGGER IF EXISTS trg_compensation_salary_plaintext_guard ON payroll.employee_compensations;
            DROP FUNCTION IF EXISTS payroll.prevent_plaintext_salary_restore();
            DROP TRIGGER IF EXISTS trg_compensation_salary_secret_clear_plaintext ON payroll.compensation_salary_secrets;
            DROP FUNCTION IF EXISTS payroll.clear_plaintext_salary_after_secret();
            DROP TABLE IF EXISTS payroll.compensation_salary_secrets;
            DROP TABLE IF EXISTS hr.employee_sensitive_profiles;
            DROP TABLE IF EXISTS system.mfa_challenges;
            DROP TABLE IF EXISTS system.user_mfa_credentials;
            ALTER TABLE system.refresh_tokens DROP COLUMN IF EXISTS mfa_verified;
            """);
    }
}
