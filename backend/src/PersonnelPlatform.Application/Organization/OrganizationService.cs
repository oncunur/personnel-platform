using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Organization;

namespace PersonnelPlatform.Application.Organization;

public sealed class OrganizationService(
    IOrganizationRepository repository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CompanySummary>> ListCompaniesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        var rows = await repository.ListCompaniesAsync(access.Global, access.CompanyIds, cancellationToken);
        return rows.Select(ToCompany).ToArray();
    }

    public async Task<OrganizationResult<CompanySummary>> CreateCompanyAsync(Guid userId, CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (!access.Global) return OrganizationResult<CompanySummary>.Failure("SCOPE_DENIED", "Yeni şirket oluşturmak için GLOBAL scope gereklidir.");
        if (!ValidCodeName(request.Code, request.Name)) return OrganizationResult<CompanySummary>.Failure("COMPANY_DATA_INVALID", "Şirket kodu ve adı zorunludur.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repository.CompanyCodeExistsAsync(code, cancellationToken)) return OrganizationResult<CompanySummary>.Failure("COMPANY_CODE_ALREADY_EXISTS", "Şirket kodu zaten kullanılıyor.");

        try
        {
            var company = Company.Create(code, request.Name, request.TaxNumber, request.Phone, request.Email, request.Address, request.DefaultCurrency ?? "TRY", timeProvider.GetUtcNow(), userId);
            repository.AddCompany(company);
            await repository.SaveChangesAsync(cancellationToken);
            return OrganizationResult<CompanySummary>.Success(ToCompany(company));
        }
        catch (ArgumentException)
        {
            return OrganizationResult<CompanySummary>.Failure("COMPANY_DATA_INVALID", "Şirket bilgileri geçersiz.");
        }
    }

    public async Task<OrganizationResult<IReadOnlyList<BranchSummary>>> ListBranchesAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, companyId, cancellationToken)) return OrganizationResult<IReadOnlyList<BranchSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListBranchesAsync(companyId, cancellationToken);
        return OrganizationResult<IReadOnlyList<BranchSummary>>.Success(rows.Select(ToBranch).ToArray());
    }

    public async Task<OrganizationResult<BranchSummary>> CreateBranchAsync(Guid userId, CreateBranchRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, request.CompanyId, cancellationToken)) return OrganizationResult<BranchSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var company = await repository.FindCompanyAsync(request.CompanyId, cancellationToken);
        if (company is null) return OrganizationResult<BranchSummary>.Failure("COMPANY_NOT_FOUND", "Şirket bulunamadı.");
        if (!company.IsActive) return OrganizationResult<BranchSummary>.Failure("COMPANY_INACTIVE", "Pasif şirkete şube eklenemez.");
        if (!ValidCodeName(request.Code, request.Name)) return OrganizationResult<BranchSummary>.Failure("BRANCH_DATA_INVALID", "Şube kodu ve adı zorunludur.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repository.BranchCodeExistsAsync(request.CompanyId, code, cancellationToken)) return OrganizationResult<BranchSummary>.Failure("BRANCH_CODE_ALREADY_EXISTS", "Bu şirket içinde şube kodu zaten kullanılıyor.");

        var branch = Branch.Create(request.CompanyId, code, request.Name, request.Location, request.Address, timeProvider.GetUtcNow(), userId);
        repository.AddBranch(branch);
        await repository.SaveChangesAsync(cancellationToken);
        return OrganizationResult<BranchSummary>.Success(ToBranch(branch));
    }

    public async Task<OrganizationResult<IReadOnlyList<DepartmentSummary>>> ListDepartmentsAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, companyId, cancellationToken)) return OrganizationResult<IReadOnlyList<DepartmentSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListDepartmentsAsync(companyId, cancellationToken);
        return OrganizationResult<IReadOnlyList<DepartmentSummary>>.Success(rows.Select(ToDepartment).ToArray());
    }

    public async Task<OrganizationResult<DepartmentSummary>> CreateDepartmentAsync(Guid userId, CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, request.CompanyId, cancellationToken)) return OrganizationResult<DepartmentSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var company = await repository.FindCompanyAsync(request.CompanyId, cancellationToken);
        if (company is null) return OrganizationResult<DepartmentSummary>.Failure("COMPANY_NOT_FOUND", "Şirket bulunamadı.");
        if (!company.IsActive) return OrganizationResult<DepartmentSummary>.Failure("COMPANY_INACTIVE", "Pasif şirkete departman eklenemez.");

        if (request.BranchId is not null)
        {
            var branch = await repository.FindBranchAsync(request.BranchId.Value, cancellationToken);
            if (branch is null) return OrganizationResult<DepartmentSummary>.Failure("BRANCH_NOT_FOUND", "Şube bulunamadı.");
            if (branch.CompanyId != request.CompanyId) return OrganizationResult<DepartmentSummary>.Failure("BRANCH_COMPANY_MISMATCH", "Şube seçilen şirkete ait değil.");
        }

        if (request.ParentDepartmentId is not null)
        {
            var parent = await repository.FindDepartmentAsync(request.ParentDepartmentId.Value, cancellationToken);
            if (parent is null) return OrganizationResult<DepartmentSummary>.Failure("DEPARTMENT_NOT_FOUND", "Üst departman bulunamadı.");
            if (parent.CompanyId != request.CompanyId) return OrganizationResult<DepartmentSummary>.Failure("ORGANIZATION_RELATION_MISMATCH", "Üst departman farklı bir şirkete ait.");
        }

        if (!ValidCodeName(request.Code, request.Name)) return OrganizationResult<DepartmentSummary>.Failure("DEPARTMENT_DATA_INVALID", "Departman kodu ve adı zorunludur.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repository.DepartmentCodeExistsAsync(request.CompanyId, code, cancellationToken)) return OrganizationResult<DepartmentSummary>.Failure("DEPARTMENT_CODE_ALREADY_EXISTS", "Departman kodu zaten kullanılıyor.");

        var department = Department.Create(request.CompanyId, request.BranchId, request.ParentDepartmentId, code, request.Name, timeProvider.GetUtcNow(), userId);
        repository.AddDepartment(department);
        await repository.SaveChangesAsync(cancellationToken);
        return OrganizationResult<DepartmentSummary>.Success(ToDepartment(department));
    }

    public async Task<OrganizationResult<IReadOnlyList<PositionSummary>>> ListPositionsAsync(Guid userId, Guid departmentId, CancellationToken cancellationToken)
    {
        var department = await repository.FindDepartmentAsync(departmentId, cancellationToken);
        if (department is null) return OrganizationResult<IReadOnlyList<PositionSummary>>.Failure("DEPARTMENT_NOT_FOUND", "Departman bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, department.CompanyId, cancellationToken)) return OrganizationResult<IReadOnlyList<PositionSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListPositionsAsync(departmentId, cancellationToken);
        return OrganizationResult<IReadOnlyList<PositionSummary>>.Success(rows.Select(ToPosition).ToArray());
    }

    public async Task<OrganizationResult<PositionSummary>> CreatePositionAsync(Guid userId, CreatePositionRequest request, CancellationToken cancellationToken)
    {
        var department = await repository.FindDepartmentAsync(request.DepartmentId, cancellationToken);
        if (department is null) return OrganizationResult<PositionSummary>.Failure("DEPARTMENT_NOT_FOUND", "Departman bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, department.CompanyId, cancellationToken)) return OrganizationResult<PositionSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (!department.IsActive) return OrganizationResult<PositionSummary>.Failure("DEPARTMENT_INACTIVE", "Pasif departmana pozisyon eklenemez.");
        if (!ValidCodeName(request.Code, request.Name)) return OrganizationResult<PositionSummary>.Failure("POSITION_DATA_INVALID", "Pozisyon kodu ve adı zorunludur.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repository.PositionCodeExistsAsync(request.DepartmentId, code, cancellationToken)) return OrganizationResult<PositionSummary>.Failure("POSITION_CODE_ALREADY_EXISTS", "Pozisyon kodu bu departmanda zaten kullanılıyor.");

        var position = Position.Create(request.DepartmentId, code, request.Name, timeProvider.GetUtcNow(), userId);
        repository.AddPosition(position);
        await repository.SaveChangesAsync(cancellationToken);
        return OrganizationResult<PositionSummary>.Success(ToPosition(position));
    }

    public async Task<OrganizationResult<IReadOnlyList<ProjectSummary>>> ListProjectsAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, companyId, cancellationToken)) return OrganizationResult<IReadOnlyList<ProjectSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListProjectsAsync(companyId, cancellationToken);
        return OrganizationResult<IReadOnlyList<ProjectSummary>>.Success(rows.Select(ToProject).ToArray());
    }

    public async Task<OrganizationResult<ProjectSummary>> CreateProjectAsync(Guid userId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, request.CompanyId, cancellationToken)) return OrganizationResult<ProjectSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var company = await repository.FindCompanyAsync(request.CompanyId, cancellationToken);
        if (company is null) return OrganizationResult<ProjectSummary>.Failure("COMPANY_NOT_FOUND", "Şirket bulunamadı.");
        if (!company.IsActive) return OrganizationResult<ProjectSummary>.Failure("COMPANY_INACTIVE", "Pasif şirkete proje eklenemez.");
        if (!ValidCodeName(request.Code, request.Name)) return OrganizationResult<ProjectSummary>.Failure("PROJECT_DATA_INVALID", "Proje kodu ve adı zorunludur.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repository.ProjectCodeExistsAsync(request.CompanyId, code, cancellationToken)) return OrganizationResult<ProjectSummary>.Failure("PROJECT_CODE_ALREADY_EXISTS", "Proje kodu bu şirkette zaten kullanılıyor.");

        try
        {
            var project = Project.Create(request.CompanyId, code, request.Name, request.Location, request.CountryCode, request.StartDate, request.PlannedEndDate, timeProvider.GetUtcNow(), userId);
            repository.AddProject(project);
            await repository.SaveChangesAsync(cancellationToken);
            return OrganizationResult<ProjectSummary>.Success(ToProject(project));
        }
        catch (ArgumentException)
        {
            return OrganizationResult<ProjectSummary>.Failure("PROJECT_DATE_INVALID", "Proje tarihleri veya ülke kodu geçersiz.");
        }
    }

    public async Task<OrganizationResult<IReadOnlyList<CostCenterSummary>>> ListCostCentersAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, companyId, cancellationToken)) return OrganizationResult<IReadOnlyList<CostCenterSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var rows = await repository.ListCostCentersAsync(companyId, cancellationToken);
        return OrganizationResult<IReadOnlyList<CostCenterSummary>>.Success(rows.Select(ToCostCenter).ToArray());
    }

    public async Task<OrganizationResult<CostCenterSummary>> CreateCostCenterAsync(Guid userId, CreateCostCenterRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, request.CompanyId, cancellationToken)) return OrganizationResult<CostCenterSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var company = await repository.FindCompanyAsync(request.CompanyId, cancellationToken);
        if (company is null) return OrganizationResult<CostCenterSummary>.Failure("COMPANY_NOT_FOUND", "Şirket bulunamadı.");

        if (request.ProjectId is not null)
        {
            var project = await repository.FindProjectAsync(request.ProjectId.Value, cancellationToken);
            if (project is null) return OrganizationResult<CostCenterSummary>.Failure("PROJECT_NOT_FOUND", "Proje bulunamadı.");
            if (project.CompanyId != request.CompanyId) return OrganizationResult<CostCenterSummary>.Failure("ORGANIZATION_RELATION_MISMATCH", "Proje farklı bir şirkete ait.");
        }

        if (request.ParentCostCenterId is not null)
        {
            var parent = await repository.FindCostCenterAsync(request.ParentCostCenterId.Value, cancellationToken);
            if (parent is null) return OrganizationResult<CostCenterSummary>.Failure("COST_CENTER_NOT_FOUND", "Üst cost center bulunamadı.");
            if (parent.CompanyId != request.CompanyId) return OrganizationResult<CostCenterSummary>.Failure("ORGANIZATION_RELATION_MISMATCH", "Üst cost center farklı bir şirkete ait.");
        }

        if (!ValidCodeName(request.Code, request.Name)) return OrganizationResult<CostCenterSummary>.Failure("COST_CENTER_DATA_INVALID", "Cost center kodu ve adı zorunludur.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repository.CostCenterCodeExistsAsync(request.CompanyId, code, cancellationToken)) return OrganizationResult<CostCenterSummary>.Failure("COST_CENTER_CODE_ALREADY_EXISTS", "Cost center kodu bu şirkette zaten kullanılıyor.");

        var costCenter = CostCenter.Create(request.CompanyId, request.ProjectId, request.ParentCostCenterId, code, request.Name, timeProvider.GetUtcNow(), userId);
        repository.AddCostCenter(costCenter);
        await repository.SaveChangesAsync(cancellationToken);
        return OrganizationResult<CostCenterSummary>.Success(ToCostCenter(costCenter));
    }

    private async Task<bool> CanAccessCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) =>
        await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, cancellationToken);

    private async Task<CompanyAccess> ResolveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        var global = snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global);
        var companyIds = snapshot.Scopes
            .Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null)
            .Select(x => x.ScopeId!.Value)
            .Distinct()
            .ToArray();
        return new CompanyAccess(global, companyIds);
    }

    private static bool ValidCodeName(string? code, string? name) =>
        !string.IsNullOrWhiteSpace(code) && code.Trim().Length <= 100 && !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200;

    private static CompanySummary ToCompany(Company x) => new(x.Id, x.Code, x.Name, x.DefaultCurrency, x.IsActive, x.Version);
    private static BranchSummary ToBranch(Branch x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.Location, x.IsActive, x.Version);
    private static DepartmentSummary ToDepartment(Department x) => new(x.Id, x.CompanyId, x.BranchId, x.ParentDepartmentId, x.Code, x.Name, x.IsActive, x.Version);
    private static PositionSummary ToPosition(Position x) => new(x.Id, x.DepartmentId, x.Code, x.Name, x.IsActive, x.Version);
    private static ProjectSummary ToProject(Project x) => new(x.Id, x.CompanyId, x.Code, x.Name, x.Status, x.Location, x.CountryCode, x.StartDate, x.PlannedEndDate, x.IsActive, x.Version);
    private static CostCenterSummary ToCostCenter(CostCenter x) => new(x.Id, x.CompanyId, x.ProjectId, x.ParentCostCenterId, x.Code, x.Name, x.IsActive, x.Version);

    private sealed record CompanyAccess(bool Global, IReadOnlyCollection<Guid> CompanyIds);
}
