using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Domain.Audit;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    public DbSet<EmployeeType> EmployeeTypes => Set<EmployeeType>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeProjectAssignment> EmployeeProjectAssignments => Set<EmployeeProjectAssignment>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentTypeEmployeeTypeRequirement> DocumentTypeEmployeeTypeRequirements => Set<DocumentTypeEmployeeTypeRequirement>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
