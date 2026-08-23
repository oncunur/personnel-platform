using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Personnel;

public interface IPersonnelRepository
{
    Task<IReadOnlyList<EmployeeType>> ListEmployeeTypesAsync(CancellationToken cancellationToken);
    Task<EmployeeType?> FindEmployeeTypeAsync(Guid id, CancellationToken cancellationToken);
    Task<Employee?> FindEmployeeAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> EmployeeNoExistsAsync(Guid companyId, string employeeNo, Guid? exceptEmployeeId, CancellationToken cancellationToken);
    Task<PagedResult<EmployeeListItem>> SearchEmployeesAsync(EmployeeQuery query, bool globalAccess, IReadOnlyCollection<Guid> allowedCompanyIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeProjectAssignment>> ListProjectAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmployeeProjectAssignment>> ListOverlappingAssignmentsAsync(Guid employeeId, DateOnly from, DateOnly? until, CancellationToken cancellationToken);
    void AddEmployee(Employee employee);
    void AddProjectAssignment(EmployeeProjectAssignment assignment);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
