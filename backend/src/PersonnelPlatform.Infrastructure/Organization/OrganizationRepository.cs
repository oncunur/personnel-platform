using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Organization;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Organization;

public sealed class OrganizationRepository(ApplicationDbContext dbContext) : IOrganizationRepository
{
    public async Task<IReadOnlyList<Company>> ListCompaniesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
    {
        var query = dbContext.Companies.Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.Id));
        return await query.OrderBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public Task<Company?> FindCompanyAsync(Guid id, CancellationToken cancellationToken) => dbContext.Companies.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
    public Task<bool> CompanyCodeExistsAsync(string code, CancellationToken cancellationToken) => dbContext.Companies.AnyAsync(x => x.Code == code && x.DeletedAt == null, cancellationToken);
    public void AddCompany(Company company) => dbContext.Companies.Add(company);

    public async Task<IReadOnlyList<Branch>> ListBranchesAsync(Guid companyId, CancellationToken cancellationToken) => await dbContext.Branches.Where(x => x.CompanyId == companyId && x.DeletedAt == null).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    public Task<Branch?> FindBranchAsync(Guid id, CancellationToken cancellationToken) => dbContext.Branches.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
    public Task<bool> BranchCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken) => dbContext.Branches.AnyAsync(x => x.CompanyId == companyId && x.Code == code && x.DeletedAt == null, cancellationToken);
    public void AddBranch(Branch branch) => dbContext.Branches.Add(branch);

    public async Task<IReadOnlyList<Department>> ListDepartmentsAsync(Guid companyId, CancellationToken cancellationToken) => await dbContext.Departments.Where(x => x.CompanyId == companyId && x.DeletedAt == null).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    public Task<Department?> FindDepartmentAsync(Guid id, CancellationToken cancellationToken) => dbContext.Departments.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
    public Task<bool> DepartmentCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken) => dbContext.Departments.AnyAsync(x => x.CompanyId == companyId && x.Code == code && x.DeletedAt == null, cancellationToken);
    public void AddDepartment(Department department) => dbContext.Departments.Add(department);

    public async Task<IReadOnlyList<Position>> ListPositionsAsync(Guid departmentId, CancellationToken cancellationToken) => await dbContext.Positions.Where(x => x.DepartmentId == departmentId && x.DeletedAt == null).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    public Task<Position?> FindPositionAsync(Guid id, CancellationToken cancellationToken) => dbContext.Positions.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
    public Task<bool> PositionCodeExistsAsync(Guid departmentId, string code, CancellationToken cancellationToken) => dbContext.Positions.AnyAsync(x => x.DepartmentId == departmentId && x.Code == code && x.DeletedAt == null, cancellationToken);
    public void AddPosition(Position position) => dbContext.Positions.Add(position);

    public async Task<IReadOnlyList<Project>> ListProjectsAsync(Guid companyId, CancellationToken cancellationToken) => await dbContext.Projects.Where(x => x.CompanyId == companyId && x.DeletedAt == null).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    public Task<Project?> FindProjectAsync(Guid id, CancellationToken cancellationToken) => dbContext.Projects.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
    public Task<bool> ProjectCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken) => dbContext.Projects.AnyAsync(x => x.CompanyId == companyId && x.Code == code && x.DeletedAt == null, cancellationToken);
    public void AddProject(Project project) => dbContext.Projects.Add(project);

    public async Task<IReadOnlyList<CostCenter>> ListCostCentersAsync(Guid companyId, CancellationToken cancellationToken) => await dbContext.CostCenters.Where(x => x.CompanyId == companyId && x.DeletedAt == null).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    public Task<CostCenter?> FindCostCenterAsync(Guid id, CancellationToken cancellationToken) => dbContext.CostCenters.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
    public Task<bool> CostCenterCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken) => dbContext.CostCenters.AnyAsync(x => x.CompanyId == companyId && x.Code == code && x.DeletedAt == null, cancellationToken);
    public void AddCostCenter(CostCenter costCenter) => dbContext.CostCenters.Add(costCenter);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
