using PersonnelPlatform.Domain.Organization;

namespace PersonnelPlatform.Application.Organization;

public interface IOrganizationRepository
{
    Task<IReadOnlyList<Company>> ListCompaniesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken);
    Task<Company?> FindCompanyAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CompanyCodeExistsAsync(string code, CancellationToken cancellationToken);
    void AddCompany(Company company);

    Task<IReadOnlyList<Branch>> ListBranchesAsync(Guid companyId, CancellationToken cancellationToken);
    Task<Branch?> FindBranchAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> BranchCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken);
    void AddBranch(Branch branch);

    Task<IReadOnlyList<Department>> ListDepartmentsAsync(Guid companyId, CancellationToken cancellationToken);
    Task<Department?> FindDepartmentAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> DepartmentCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken);
    void AddDepartment(Department department);

    Task<IReadOnlyList<Position>> ListPositionsAsync(Guid departmentId, CancellationToken cancellationToken);
    Task<Position?> FindPositionAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> PositionCodeExistsAsync(Guid departmentId, string code, CancellationToken cancellationToken);
    void AddPosition(Position position);

    Task<IReadOnlyList<Project>> ListProjectsAsync(Guid companyId, CancellationToken cancellationToken);
    Task<Project?> FindProjectAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ProjectCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken);
    void AddProject(Project project);

    Task<IReadOnlyList<CostCenter>> ListCostCentersAsync(Guid companyId, CancellationToken cancellationToken);
    Task<CostCenter?> FindCostCenterAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CostCenterCodeExistsAsync(Guid companyId, string code, CancellationToken cancellationToken);
    void AddCostCenter(CostCenter costCenter);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
