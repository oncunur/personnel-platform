using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230005_OrganizationCore")]
public sealed class OrganizationCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE organization.companies (
                id uuid NOT NULL CONSTRAINT pk_companies PRIMARY KEY,
                code varchar(100) NOT NULL,
                name varchar(200) NOT NULL,
                tax_number varchar(50) NULL,
                phone varchar(50) NULL,
                email varchar(320) NULL,
                address varchar(1000) NULL,
                default_currency varchar(3) NOT NULL DEFAULT 'TRY',
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1
            );
            CREATE UNIQUE INDEX ux_companies_code ON organization.companies(code);

            CREATE TABLE organization.branches (
                id uuid NOT NULL CONSTRAINT pk_branches PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(100) NOT NULL,
                name varchar(200) NOT NULL,
                location varchar(200) NULL,
                address varchar(1000) NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_branches_companies_company_id FOREIGN KEY (company_id)
                    REFERENCES organization.companies(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_branches_company_code ON organization.branches(company_id, code);
            CREATE INDEX ix_branches_company_id ON organization.branches(company_id);

            CREATE TABLE organization.departments (
                id uuid NOT NULL CONSTRAINT pk_departments PRIMARY KEY,
                company_id uuid NOT NULL,
                branch_id uuid NULL,
                parent_department_id uuid NULL,
                manager_employee_id uuid NULL,
                code varchar(100) NOT NULL,
                name varchar(200) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_departments_companies_company_id FOREIGN KEY (company_id)
                    REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_departments_branches_branch_id FOREIGN KEY (branch_id)
                    REFERENCES organization.branches(id) ON DELETE RESTRICT,
                CONSTRAINT fk_departments_departments_parent_department_id FOREIGN KEY (parent_department_id)
                    REFERENCES organization.departments(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_departments_company_code ON organization.departments(company_id, code);
            CREATE INDEX ix_departments_company_id ON organization.departments(company_id);
            CREATE INDEX ix_departments_branch_id ON organization.departments(branch_id);
            CREATE INDEX ix_departments_parent_id ON organization.departments(parent_department_id);

            CREATE TABLE organization.positions (
                id uuid NOT NULL CONSTRAINT pk_positions PRIMARY KEY,
                department_id uuid NOT NULL,
                code varchar(100) NOT NULL,
                name varchar(200) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_positions_departments_department_id FOREIGN KEY (department_id)
                    REFERENCES organization.departments(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_positions_department_code ON organization.positions(department_id, code);
            CREATE INDEX ix_positions_department_id ON organization.positions(department_id);

            CREATE TABLE organization.projects (
                id uuid NOT NULL CONSTRAINT pk_projects PRIMARY KEY,
                company_id uuid NOT NULL,
                code varchar(100) NOT NULL,
                name varchar(200) NOT NULL,
                location varchar(200) NULL,
                country_code varchar(3) NULL,
                start_date date NULL,
                planned_end_date date NULL,
                actual_end_date date NULL,
                manager_employee_id uuid NULL,
                default_cost_center_id uuid NULL,
                status varchar(30) NOT NULL DEFAULT 'DRAFT',
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_projects_companies_company_id FOREIGN KEY (company_id)
                    REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT ck_projects_dates CHECK (start_date IS NULL OR planned_end_date IS NULL OR planned_end_date >= start_date),
                CONSTRAINT ck_projects_status CHECK (status IN ('DRAFT','PLANNED','ACTIVE','ON_HOLD','CLOSING','COMPLETED','CANCELLED'))
            );
            CREATE UNIQUE INDEX ux_projects_company_code ON organization.projects(company_id, code);
            CREATE INDEX ix_projects_company_id ON organization.projects(company_id);
            CREATE INDEX ix_projects_status ON organization.projects(status);

            CREATE TABLE organization.cost_centers (
                id uuid NOT NULL CONSTRAINT pk_cost_centers PRIMARY KEY,
                company_id uuid NOT NULL,
                project_id uuid NULL,
                parent_cost_center_id uuid NULL,
                code varchar(100) NOT NULL,
                name varchar(200) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_cost_centers_companies_company_id FOREIGN KEY (company_id)
                    REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_cost_centers_projects_project_id FOREIGN KEY (project_id)
                    REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_cost_centers_cost_centers_parent_id FOREIGN KEY (parent_cost_center_id)
                    REFERENCES organization.cost_centers(id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX ux_cost_centers_company_code ON organization.cost_centers(company_id, code);
            CREATE INDEX ix_cost_centers_company_id ON organization.cost_centers(company_id);
            CREATE INDEX ix_cost_centers_project_id ON organization.cost_centers(project_id);
            CREATE INDEX ix_cost_centers_parent_id ON organization.cost_centers(parent_cost_center_id);

            ALTER TABLE organization.projects
                ADD CONSTRAINT fk_projects_cost_centers_default_cost_center_id
                FOREIGN KEY (default_cost_center_id) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT;
            CREATE INDEX ix_projects_default_cost_center_id ON organization.projects(default_cost_center_id);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000009', 'organization.company.view', 'View Companies', 'Organization', 'View companies in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000010', 'organization.company.manage', 'Manage Companies', 'Organization', 'Create and manage companies.', TRUE),
                ('20000000-0000-0000-0000-000000000011', 'organization.branch.view', 'View Branches', 'Organization', 'View branches.', TRUE),
                ('20000000-0000-0000-0000-000000000012', 'organization.branch.manage', 'Manage Branches', 'Organization', 'Create and manage branches.', TRUE),
                ('20000000-0000-0000-0000-000000000013', 'organization.department.view', 'View Departments', 'Organization', 'View departments.', TRUE),
                ('20000000-0000-0000-0000-000000000014', 'organization.department.manage', 'Manage Departments', 'Organization', 'Create and manage departments.', TRUE),
                ('20000000-0000-0000-0000-000000000015', 'organization.position.view', 'View Positions', 'Organization', 'View positions.', TRUE),
                ('20000000-0000-0000-0000-000000000016', 'organization.position.manage', 'Manage Positions', 'Organization', 'Create and manage positions.', TRUE),
                ('20000000-0000-0000-0000-000000000017', 'organization.project.view', 'View Projects', 'Organization', 'View projects.', TRUE),
                ('20000000-0000-0000-0000-000000000018', 'organization.project.manage', 'Manage Projects', 'Organization', 'Create and manage projects.', TRUE),
                ('20000000-0000-0000-0000-000000000019', 'organization.costcenter.view', 'View Cost Centers', 'Organization', 'View cost centers.', TRUE),
                ('20000000-0000-0000-0000-000000000020', 'organization.costcenter.manage', 'Manage Cost Centers', 'Organization', 'Create and manage cost centers.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000010', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000009', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000011', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000010', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000011', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000012', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000014', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000013', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000015', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000014', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000016', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000015', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000017', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000016', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000018', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000017', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000019', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000018', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000020', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000019', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000021', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000020', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000010','30000000-0000-0000-0000-000000000011','30000000-0000-0000-0000-000000000012',
                '30000000-0000-0000-0000-000000000013','30000000-0000-0000-0000-000000000014','30000000-0000-0000-0000-000000000015',
                '30000000-0000-0000-0000-000000000016','30000000-0000-0000-0000-000000000017','30000000-0000-0000-0000-000000000018',
                '30000000-0000-0000-0000-000000000019','30000000-0000-0000-0000-000000000020','30000000-0000-0000-0000-000000000021');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000009','20000000-0000-0000-0000-000000000010','20000000-0000-0000-0000-000000000011',
                '20000000-0000-0000-0000-000000000012','20000000-0000-0000-0000-000000000013','20000000-0000-0000-0000-000000000014',
                '20000000-0000-0000-0000-000000000015','20000000-0000-0000-0000-000000000016','20000000-0000-0000-0000-000000000017',
                '20000000-0000-0000-0000-000000000018','20000000-0000-0000-0000-000000000019','20000000-0000-0000-0000-000000000020');
            ALTER TABLE organization.projects DROP CONSTRAINT IF EXISTS fk_projects_cost_centers_default_cost_center_id;
            DROP TABLE IF EXISTS organization.cost_centers;
            DROP TABLE IF EXISTS organization.projects;
            DROP TABLE IF EXISTS organization.positions;
            DROP TABLE IF EXISTS organization.departments;
            DROP TABLE IF EXISTS organization.branches;
            DROP TABLE IF EXISTS organization.companies;
            """);
    }
}
