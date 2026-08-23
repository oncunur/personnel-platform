using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Personnel;

public sealed class PersonnelRepository(ApplicationDbContext dbContext) : IPersonnelRepository
{
    public async Task<IReadOnlyList<EmployeeType>> ListEmployeeTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.EmployeeTypes.Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code).ToListAsync(cancellationToken);

    public Task<EmployeeType?> FindEmployeeTypeAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.EmployeeTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Employee?> FindEmployeeAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Employees.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

    public Task<bool> EmployeeNoExistsAsync(Guid companyId, string employeeNo, Guid? exceptEmployeeId, CancellationToken cancellationToken) =>
        dbContext.Employees.AnyAsync(x => x.CompanyId == companyId && x.EmployeeNo == employeeNo && x.DeletedAt == null && (exceptEmployeeId == null || x.Id != exceptEmployeeId), cancellationToken);

    public async Task<PagedResult<EmployeeListItem>> SearchEmployeesAsync(EmployeeQuery query, bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken)
    {
        var rows = dbContext.Employees.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) rows = rows.Where(x => allowedCompanyIds.Contains(x.CompanyId));
        if (query.CompanyId is not null) rows = rows.Where(x => x.CompanyId == query.CompanyId.Value);
        if (query.BranchId is not null) rows = rows.Where(x => x.BranchId == query.BranchId.Value);
        if (query.DepartmentId is not null) rows = rows.Where(x => x.DepartmentId == query.DepartmentId.Value);
        if (query.PositionId is not null) rows = rows.Where(x => x.PositionId == query.PositionId.Value);
        if (query.EmployeeTypeId is not null) rows = rows.Where(x => x.EmployeeTypeId == query.EmployeeTypeId.Value);
        if (query.Status is not null) rows = rows.Where(x => x.Status == query.Status);
        if (query.ProjectId is not null)
        {
            var projectId = query.ProjectId.Value;
            rows = rows.Where(employee => dbContext.EmployeeProjectAssignments.Any(a => a.EmployeeId == employee.Id && a.ProjectId == projectId && a.Status == ProjectAssignmentStatuses.Active && a.DeletedAt == null));
        }
        if (query.Search is not null)
        {
            var term = $"%{query.Search}%";
            rows = rows.Where(x => EF.Functions.ILike(x.EmployeeNo, term) || EF.Functions.ILike(x.FirstName, term) || EF.Functions.ILike(x.LastName, term));
        }

        rows = (query.Sort ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "name" => rows.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.EmployeeNo),
            "-name" => rows.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName).ThenBy(x => x.EmployeeNo),
            "hiredate" => rows.OrderBy(x => x.HireDate).ThenBy(x => x.EmployeeNo),
            "-hiredate" => rows.OrderByDescending(x => x.HireDate).ThenBy(x => x.EmployeeNo),
            _ => rows.OrderBy(x => x.EmployeeNo)
        };

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new EmployeeListItem(x.Id, x.EmployeeNo, x.FirstName, x.LastName, x.Status, x.CompanyId, x.BranchId, x.DepartmentId, x.PositionId, x.EmployeeTypeId, x.HireDate, x.Version))
            .ToListAsync(cancellationToken);
        return new PagedResult<EmployeeListItem>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<EmployeeProjectAssignment>> ListProjectAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken) =>
        await dbContext.EmployeeProjectAssignments.Where(x => x.EmployeeId == employeeId && x.DeletedAt == null).OrderByDescending(x => x.ValidFrom).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EmployeeProjectAssignment>> ListOverlappingAssignmentsAsync(Guid employeeId, DateOnly from, DateOnly? until, CancellationToken cancellationToken)
    {
        var effectiveUntil = until ?? DateOnly.MaxValue;
        return await dbContext.EmployeeProjectAssignments
            .Where(x => x.EmployeeId == employeeId && x.DeletedAt == null && x.ValidFrom <= effectiveUntil && (x.ValidUntil == null || x.ValidUntil >= from))
            .ToListAsync(cancellationToken);
    }

    public void AddEmployee(Employee employee) => dbContext.Employees.Add(employee);
    public void AddProjectAssignment(EmployeeProjectAssignment assignment) => dbContext.EmployeeProjectAssignments.Add(assignment);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
