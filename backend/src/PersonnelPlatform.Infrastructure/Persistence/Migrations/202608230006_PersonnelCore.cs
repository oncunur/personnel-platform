using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonnelPlatform.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202608230006_PersonnelCore")]
public sealed class PersonnelCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE EXTENSION IF NOT EXISTS btree_gist;

            CREATE TABLE hr.employee_types (
                id uuid NOT NULL CONSTRAINT pk_employee_types PRIMARY KEY,
                code varchar(50) NOT NULL,
                name varchar(100) NOT NULL,
                is_active boolean NOT NULL DEFAULT TRUE,
                display_order integer NOT NULL DEFAULT 0
            );
            CREATE UNIQUE INDEX ux_employee_types_code ON hr.employee_types(code);

            INSERT INTO hr.employee_types (id, code, name, is_active, display_order) VALUES
                ('40000000-0000-0000-0000-000000000001', 'BLUE_COLLAR', 'Mavi Yaka', TRUE, 10),
                ('40000000-0000-0000-0000-000000000002', 'WHITE_COLLAR', 'Beyaz Yaka', TRUE, 20),
                ('40000000-0000-0000-0000-000000000003', 'INTERN', 'Stajyer', TRUE, 30),
                ('40000000-0000-0000-0000-000000000004', 'CONTRACTOR', 'Yüklenici Personeli', TRUE, 40);

            CREATE TABLE hr.employees (
                id uuid NOT NULL CONSTRAINT pk_employees PRIMARY KEY,
                company_id uuid NOT NULL,
                branch_id uuid NULL,
                department_id uuid NOT NULL,
                position_id uuid NOT NULL,
                employee_type_id uuid NOT NULL,
                manager_employee_id uuid NULL,
                employee_no varchar(50) NOT NULL,
                first_name varchar(100) NOT NULL,
                last_name varchar(100) NOT NULL,
                preferred_name varchar(100) NULL,
                birth_date date NULL,
                phone varchar(50) NULL,
                email varchar(320) NULL,
                hire_date date NOT NULL,
                termination_date date NULL,
                status varchar(30) NOT NULL DEFAULT 'ACTIVE',
                notes varchar(2000) NULL,
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_employees_companies_company_id FOREIGN KEY (company_id) REFERENCES organization.companies(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employees_branches_branch_id FOREIGN KEY (branch_id) REFERENCES organization.branches(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employees_departments_department_id FOREIGN KEY (department_id) REFERENCES organization.departments(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employees_positions_position_id FOREIGN KEY (position_id) REFERENCES organization.positions(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employees_employee_types_employee_type_id FOREIGN KEY (employee_type_id) REFERENCES hr.employee_types(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employees_employees_manager_employee_id FOREIGN KEY (manager_employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT ck_employees_status CHECK (status IN ('DRAFT','ACTIVE','SUSPENDED','TERMINATED')),
                CONSTRAINT ck_employees_manager_not_self CHECK (manager_employee_id IS NULL OR manager_employee_id <> id),
                CONSTRAINT ck_employees_birth_hire CHECK (birth_date IS NULL OR birth_date <= hire_date),
                CONSTRAINT ck_employees_termination_hire CHECK (termination_date IS NULL OR termination_date >= hire_date)
            );
            CREATE UNIQUE INDEX ux_employees_company_employee_no ON hr.employees(company_id, employee_no);
            CREATE INDEX ix_employees_company_status ON hr.employees(company_id, status);
            CREATE INDEX ix_employees_department_id ON hr.employees(department_id);
            CREATE INDEX ix_employees_position_id ON hr.employees(position_id);
            CREATE INDEX ix_employees_employee_type_id ON hr.employees(employee_type_id);
            CREATE INDEX ix_employees_manager_employee_id ON hr.employees(manager_employee_id);
            CREATE INDEX ix_employees_name_search ON hr.employees(company_id, last_name, first_name);

            CREATE TABLE hr.employee_project_assignments (
                id uuid NOT NULL CONSTRAINT pk_employee_project_assignments PRIMARY KEY,
                employee_id uuid NOT NULL,
                project_id uuid NOT NULL,
                cost_center_id uuid NULL,
                valid_from date NOT NULL,
                valid_until date NULL,
                allocation_percent numeric(5,2) NOT NULL,
                status varchar(30) NOT NULL DEFAULT 'ACTIVE',
                created_at timestamptz NOT NULL,
                created_by uuid NULL,
                updated_at timestamptz NULL,
                updated_by uuid NULL,
                deleted_at timestamptz NULL,
                deleted_by uuid NULL,
                version integer NOT NULL DEFAULT 1,
                CONSTRAINT fk_employee_project_assignments_employees_employee_id FOREIGN KEY (employee_id) REFERENCES hr.employees(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_project_assignments_projects_project_id FOREIGN KEY (project_id) REFERENCES organization.projects(id) ON DELETE RESTRICT,
                CONSTRAINT fk_employee_project_assignments_cost_centers_cost_center_id FOREIGN KEY (cost_center_id) REFERENCES organization.cost_centers(id) ON DELETE RESTRICT,
                CONSTRAINT ck_employee_project_assignments_dates CHECK (valid_until IS NULL OR valid_until >= valid_from),
                CONSTRAINT ck_employee_project_assignments_allocation CHECK (allocation_percent > 0 AND allocation_percent <= 100),
                CONSTRAINT ck_employee_project_assignments_status CHECK (status IN ('ACTIVE','CLOSED'))
            );
            CREATE INDEX ix_employee_project_assignments_employee_date ON hr.employee_project_assignments(employee_id, valid_from);
            CREATE INDEX ix_employee_project_assignments_project_date ON hr.employee_project_assignments(project_id, valid_from);
            ALTER TABLE hr.employee_project_assignments
                ADD CONSTRAINT ex_employee_project_assignment_overlap
                EXCLUDE USING gist (
                    employee_id WITH =,
                    project_id WITH =,
                    daterange(valid_from, COALESCE(valid_until, 'infinity'::date), '[]') WITH &&
                ) WHERE (status = 'ACTIVE' AND deleted_at IS NULL);

            INSERT INTO system.permissions (id, code, name, module, description, is_active) VALUES
                ('20000000-0000-0000-0000-000000000021', 'personnel.view', 'View Personnel', 'Personnel', 'View personnel in authorized scope.', TRUE),
                ('20000000-0000-0000-0000-000000000022', 'personnel.create', 'Create Personnel', 'Personnel', 'Create personnel records.', TRUE),
                ('20000000-0000-0000-0000-000000000023', 'personnel.update', 'Update Personnel', 'Personnel', 'Update personnel records and status.', TRUE),
                ('20000000-0000-0000-0000-000000000024', 'personnel.project.view', 'View Personnel Projects', 'Personnel', 'View personnel project assignments.', TRUE),
                ('20000000-0000-0000-0000-000000000025', 'personnel.project.assign', 'Assign Personnel Projects', 'Personnel', 'Create personnel project assignments.', TRUE),
                ('20000000-0000-0000-0000-000000000026', 'personnel.identity.view', 'View Sensitive Identity', 'Personnel', 'Reserved for sensitive identity fields.', TRUE);

            INSERT INTO system.role_permissions (id, role_id, permission_id, granted_at) VALUES
                ('30000000-0000-0000-0000-000000000022', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000021', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000023', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000022', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000024', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000023', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000025', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000024', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000026', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000025', '2026-08-23T00:00:00Z'),
                ('30000000-0000-0000-0000-000000000027', '10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000026', '2026-08-23T00:00:00Z');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM system.role_permissions WHERE id IN (
                '30000000-0000-0000-0000-000000000022','30000000-0000-0000-0000-000000000023','30000000-0000-0000-0000-000000000024',
                '30000000-0000-0000-0000-000000000025','30000000-0000-0000-0000-000000000026','30000000-0000-0000-0000-000000000027');
            DELETE FROM system.permissions WHERE id IN (
                '20000000-0000-0000-0000-000000000021','20000000-0000-0000-0000-000000000022','20000000-0000-0000-0000-000000000023',
                '20000000-0000-0000-0000-000000000024','20000000-0000-0000-0000-000000000025','20000000-0000-0000-0000-000000000026');
            DROP TABLE IF EXISTS hr.employee_project_assignments;
            DROP TABLE IF EXISTS hr.employees;
            DROP TABLE IF EXISTS hr.employee_types;
            """);
    }
}
