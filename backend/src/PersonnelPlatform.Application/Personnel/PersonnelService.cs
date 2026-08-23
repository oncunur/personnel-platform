using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Personnel;

public sealed class PersonnelService(
    IPersonnelRepository personnelRepository,
    IOrganizationRepository organizationRepository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<EmployeeTypeSummary>> ListEmployeeTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await personnelRepository.ListEmployeeTypesAsync(cancellationToken);
        return rows.Select(x => new EmployeeTypeSummary(x.Id, x.Code, x.Name, x.IsActive, x.DisplayOrder)).ToArray();
    }

    public async Task<PersonnelResult<PagedResult<EmployeeListItem>>> SearchAsync(Guid userId, EmployeeQuery query, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        if (query.CompanyId is not null && !access.Global && !access.CompanyIds.Contains(query.CompanyId.Value))
            return PersonnelResult<PagedResult<EmployeeListItem>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");

        var normalized = query with
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim().ToUpperInvariant(),
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100)
        };

        if (normalized.Status is not null && !EmployeeStatuses.IsKnown(normalized.Status))
            return PersonnelResult<PagedResult<EmployeeListItem>>.Failure("EMPLOYEE_STATUS_INVALID", "Personel durumu geçersiz.");

        return PersonnelResult<PagedResult<EmployeeListItem>>.Success(
            await personnelRepository.SearchEmployeesAsync(normalized, access.Global, access.CompanyIds, cancellationToken));
    }

    public async Task<PersonnelResult<EmployeeDetail>> GetAsync(Guid userId, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return PersonnelResult<EmployeeDetail>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return PersonnelResult<EmployeeDetail>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        return PersonnelResult<EmployeeDetail>.Success(ToDetail(employee));
    }

    public async Task<PersonnelResult<IReadOnlyList<EmployeeProjectAssignmentSummary>>> ListProjectAssignmentsAsync(Guid userId, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return PersonnelResult<IReadOnlyList<EmployeeProjectAssignmentSummary>>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return PersonnelResult<IReadOnlyList<EmployeeProjectAssignmentSummary>>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        var rows = await personnelRepository.ListProjectAssignmentsAsync(employeeId, cancellationToken);
        return PersonnelResult<IReadOnlyList<EmployeeProjectAssignmentSummary>>.Success(rows.Select(ToAssignment).ToArray());
    }

    public async Task<PersonnelResult<EmployeeDetail>> CreateAsync(Guid userId, CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCompanyAsync(userId, request.CompanyId, cancellationToken)) return PersonnelResult<EmployeeDetail>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var validation = await ValidateOrganizationAsync(request.CompanyId, request.BranchId, request.DepartmentId, request.PositionId, request.EmployeeTypeId, request.ManagerEmployeeId, null, cancellationToken);
        if (validation is not null) return PersonnelResult<EmployeeDetail>.Failure(validation.Value.Code, validation.Value.Message);
        if (string.IsNullOrWhiteSpace(request.EmployeeNo) || request.EmployeeNo.Trim().Length > 50) return PersonnelResult<EmployeeDetail>.Failure("EMPLOYEE_NUMBER_INVALID", "Sicil numarası zorunludur ve en fazla 50 karakter olabilir.");
        var employeeNo = request.EmployeeNo.Trim().ToUpperInvariant();
        if (await personnelRepository.EmployeeNoExistsAsync(request.CompanyId, employeeNo, null, cancellationToken)) return PersonnelResult<EmployeeDetail>.Failure("EMPLOYEE_NUMBER_ALREADY_EXISTS", "Bu sicil numarası şirkette zaten kullanılıyor.");
        if (request.BirthDate is not null && request.BirthDate > request.HireDate) return PersonnelResult<EmployeeDetail>.Failure("EMPLOYEE_DATE_INVALID", "Doğum tarihi işe giriş tarihinden sonra olamaz.");

        try
        {
            var employee = Employee.Create(request.CompanyId, request.BranchId, request.DepartmentId, request.PositionId, request.EmployeeTypeId, request.ManagerEmployeeId, employeeNo, request.FirstName, request.LastName, request.PreferredName, request.BirthDate, request.Phone, request.Email, request.HireDate, request.Notes, timeProvider.GetUtcNow(), userId);
            personnelRepository.AddEmployee(employee);
            await personnelRepository.SaveChangesAsync(cancellationToken);
            return PersonnelResult<EmployeeDetail>.Success(ToDetail(employee));
        }
        catch (ArgumentException)
        {
            return PersonnelResult<EmployeeDetail>.Failure("EMPLOYEE_DATA_INVALID", "Personel bilgileri geçersiz.");
        }
    }

    public async Task<PersonnelResult<EmployeeDetail>> UpdateAsync(Guid userId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return PersonnelResult<EmployeeDetail>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return PersonnelResult<EmployeeDetail>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (employee.Version != request.Version) return PersonnelResult<EmployeeDetail>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Kayıt başka bir kullanıcı tarafından değiştirildi. Lütfen yenileyin.");

        var validation = await ValidateOrganizationAsync(employee.CompanyId, request.BranchId, request.DepartmentId, request.PositionId, request.EmployeeTypeId, request.ManagerEmployeeId, employee.Id, cancellationToken);
        if (validation is not null) return PersonnelResult<EmployeeDetail>.Failure(validation.Value.Code, validation.Value.Message);

        try
        {
            employee.Update(request.BranchId, request.DepartmentId, request.PositionId, request.EmployeeTypeId, request.ManagerEmployeeId, request.FirstName, request.LastName, request.PreferredName, request.BirthDate, request.Phone, request.Email, request.Notes, timeProvider.GetUtcNow(), userId);
            await personnelRepository.SaveChangesAsync(cancellationToken);
            return PersonnelResult<EmployeeDetail>.Success(ToDetail(employee));
        }
        catch (ArgumentException)
        {
            return PersonnelResult<EmployeeDetail>.Failure("EMPLOYEE_DATA_INVALID", "Personel bilgileri geçersiz.");
        }
    }

    public async Task<PersonnelResult<EmployeeDetail>> SetActiveAsync(Guid userId, Guid employeeId, bool active, int version, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return PersonnelResult<EmployeeDetail>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return PersonnelResult<EmployeeDetail>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (employee.Version != version) return PersonnelResult<EmployeeDetail>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Kayıt başka bir kullanıcı tarafından değiştirildi.");

        try
        {
            if (active) employee.Activate(timeProvider.GetUtcNow(), userId); else employee.Suspend(timeProvider.GetUtcNow(), userId);
            await personnelRepository.SaveChangesAsync(cancellationToken);
            return PersonnelResult<EmployeeDetail>.Success(ToDetail(employee));
        }
        catch (InvalidOperationException)
        {
            return PersonnelResult<EmployeeDetail>.Failure("INVALID_EMPLOYEE_STATE_TRANSITION", "Personel durum geçişine izin verilmiyor.");
        }
    }

    public async Task<PersonnelResult<EmployeeProjectAssignmentSummary>> AssignProjectAsync(Guid userId, Guid employeeId, CreateEmployeeProjectAssignmentRequest request, CancellationToken cancellationToken)
    {
        var employee = await personnelRepository.FindEmployeeAsync(employeeId, cancellationToken);
        if (employee is null) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (!await CanAccessCompanyAsync(userId, employee.CompanyId, cancellationToken)) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("SCOPE_DENIED", "Personelin şirket kapsamına erişiminiz yok.");
        if (employee.Status != EmployeeStatuses.Active) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("EMPLOYEE_INACTIVE", "Yalnız aktif personele proje atanabilir.");

        var project = await organizationRepository.FindProjectAsync(request.ProjectId, cancellationToken);
        if (project is null) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("PROJECT_NOT_FOUND", "Proje bulunamadı.");
        if (project.CompanyId != employee.CompanyId) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("ORGANIZATION_RELATION_MISMATCH", "Proje personelin şirketine ait değil.");
        if (!project.IsActive) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("PROJECT_INACTIVE", "Pasif projeye personel atanamaz.");

        if (request.CostCenterId is not null)
        {
            var costCenter = await organizationRepository.FindCostCenterAsync(request.CostCenterId.Value, cancellationToken);
            if (costCenter is null) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("COST_CENTER_NOT_FOUND", "Cost center bulunamadı.");
            if (costCenter.CompanyId != employee.CompanyId || (costCenter.ProjectId is not null && costCenter.ProjectId != request.ProjectId)) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("ORGANIZATION_RELATION_MISMATCH", "Cost center proje/şirket ilişkisi geçersiz.");
        }

        if (request.ValidUntil is not null && request.ValidUntil < request.ValidFrom) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("PROJECT_ASSIGNMENT_DATE_INVALID", "Atama bitiş tarihi başlangıçtan önce olamaz.");
        if (request.AllocationPercent <= 0 || request.AllocationPercent > 100) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("PROJECT_ALLOCATION_INVALID", "Allocation yüzde 0'dan büyük ve 100'den küçük/eşit olmalıdır.");

        var overlaps = await personnelRepository.ListOverlappingAssignmentsAsync(employeeId, request.ValidFrom, request.ValidUntil, cancellationToken);
        if (overlaps.Any(x => x.ProjectId == request.ProjectId && x.Status == ProjectAssignmentStatuses.Active)) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("PROJECT_ASSIGNMENT_CONFLICT", "Aynı proje için çakışan aktif atama bulunuyor.");
        if (overlaps.Where(x => x.Status == ProjectAssignmentStatuses.Active).Sum(x => x.AllocationPercent) + request.AllocationPercent > 100) return PersonnelResult<EmployeeProjectAssignmentSummary>.Failure("PROJECT_ALLOCATION_EXCEEDED", "Çakışan proje atamalarının toplam allocation değeri %100'ü aşamaz.");

        var assignment = EmployeeProjectAssignment.Create(employeeId, request.ProjectId, request.CostCenterId, request.ValidFrom, request.ValidUntil, request.AllocationPercent, timeProvider.GetUtcNow(), userId);
        personnelRepository.AddProjectAssignment(assignment);
        await personnelRepository.SaveChangesAsync(cancellationToken);
        return PersonnelResult<EmployeeProjectAssignmentSummary>.Success(ToAssignment(assignment));
    }

    private async Task<(string Code, string Message)?> ValidateOrganizationAsync(Guid companyId, Guid? branchId, Guid departmentId, Guid positionId, Guid employeeTypeId, Guid? managerEmployeeId, Guid? employeeId, CancellationToken ct)
    {
        var company = await organizationRepository.FindCompanyAsync(companyId, ct);
        if (company is null) return ("COMPANY_NOT_FOUND", "Şirket bulunamadı.");
        if (!company.IsActive) return ("COMPANY_INACTIVE", "Şirket pasif.");

        if (branchId is not null)
        {
            var branch = await organizationRepository.FindBranchAsync(branchId.Value, ct);
            if (branch is null) return ("BRANCH_NOT_FOUND", "Şube bulunamadı.");
            if (branch.CompanyId != companyId) return ("BRANCH_COMPANY_MISMATCH", "Şube personelin şirketine ait değil.");
            if (!branch.IsActive) return ("BRANCH_INACTIVE", "Şube pasif.");
        }

        var department = await organizationRepository.FindDepartmentAsync(departmentId, ct);
        if (department is null) return ("DEPARTMENT_NOT_FOUND", "Departman bulunamadı.");
        if (department.CompanyId != companyId) return ("ORGANIZATION_RELATION_MISMATCH", "Departman personelin şirketine ait değil.");
        if (!department.IsActive) return ("DEPARTMENT_INACTIVE", "Departman pasif.");

        var position = await organizationRepository.FindPositionAsync(positionId, ct);
        if (position is null) return ("POSITION_NOT_FOUND", "Pozisyon bulunamadı.");
        if (position.DepartmentId != departmentId) return ("POSITION_DEPARTMENT_MISMATCH", "Pozisyon seçilen departmana ait değil.");
        if (!position.IsActive) return ("POSITION_INACTIVE", "Pozisyon pasif.");

        var employeeType = await personnelRepository.FindEmployeeTypeAsync(employeeTypeId, ct);
        if (employeeType is null || !employeeType.IsActive) return ("EMPLOYEE_TYPE_INVALID", "Personel tipi bulunamadı veya pasif.");

        if (managerEmployeeId is not null)
        {
            if (managerEmployeeId == employeeId) return ("EMPLOYEE_CANNOT_MANAGE_SELF", "Personel kendi yöneticisi olamaz.");
            var manager = await personnelRepository.FindEmployeeAsync(managerEmployeeId.Value, ct);
            if (manager is null || manager.Status != EmployeeStatuses.Active) return ("MANAGER_EMPLOYEE_INVALID", "Yönetici bulunamadı veya aktif değil.");
            if (manager.CompanyId != companyId) return ("ORGANIZATION_RELATION_MISMATCH", "Yönetici farklı bir şirkete ait.");
        }
        return null;
    }

    private static EmployeeDetail ToDetail(Employee employee) => new(employee.Id, employee.EmployeeNo, employee.FirstName, employee.LastName, employee.PreferredName, employee.BirthDate, employee.Phone, employee.Email, employee.Status, employee.CompanyId, employee.BranchId, employee.DepartmentId, employee.PositionId, employee.EmployeeTypeId, employee.ManagerEmployeeId, employee.HireDate, employee.TerminationDate, employee.Notes, employee.Version);
    private async Task<bool> CanAccessCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) => await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, cancellationToken);
    private async Task<CompanyAccess> ResolveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        return new CompanyAccess(snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global), snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray());
    }
    private static EmployeeProjectAssignmentSummary ToAssignment(EmployeeProjectAssignment x) => new(x.Id, x.ProjectId, x.CostCenterId, x.ValidFrom, x.ValidUntil, x.AllocationPercent, x.Status);
    private sealed record CompanyAccess(bool Global, IReadOnlyCollection<Guid> CompanyIds);
}
