namespace PersonnelPlatform.Application.Personnel;

public sealed record EmployeeTypeSummary(Guid Id, string Code, string Name, bool IsActive, int DisplayOrder);
public sealed record EmployeeListItem(
    Guid Id,
    string EmployeeNo,
    string FirstName,
    string LastName,
    string Status,
    Guid CompanyId,
    Guid? BranchId,
    Guid DepartmentId,
    Guid PositionId,
    Guid EmployeeTypeId,
    DateOnly HireDate,
    int Version);

public sealed record EmployeeDetail(
    Guid Id,
    string EmployeeNo,
    string FirstName,
    string LastName,
    string? PreferredName,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    string Status,
    Guid CompanyId,
    Guid? BranchId,
    Guid DepartmentId,
    Guid PositionId,
    Guid EmployeeTypeId,
    Guid? ManagerEmployeeId,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string? Notes,
    int Version);

public sealed record EmployeeProjectAssignmentSummary(
    Guid Id,
    Guid ProjectId,
    Guid? CostCenterId,
    DateOnly ValidFrom,
    DateOnly? ValidUntil,
    decimal AllocationPercent,
    string Status);

public sealed record CreateEmployeeRequest(
    Guid CompanyId,
    Guid? BranchId,
    Guid DepartmentId,
    Guid PositionId,
    Guid EmployeeTypeId,
    Guid? ManagerEmployeeId,
    string EmployeeNo,
    string FirstName,
    string LastName,
    string? PreferredName,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    DateOnly HireDate,
    string? Notes);

public sealed record UpdateEmployeeRequest(
    Guid? BranchId,
    Guid DepartmentId,
    Guid PositionId,
    Guid EmployeeTypeId,
    Guid? ManagerEmployeeId,
    string FirstName,
    string LastName,
    string? PreferredName,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    string? Notes,
    int Version);

public sealed record CreateEmployeeProjectAssignmentRequest(
    Guid ProjectId,
    Guid? CostCenterId,
    DateOnly ValidFrom,
    DateOnly? ValidUntil,
    decimal AllocationPercent);

public sealed record EmployeeQuery(
    string? Search,
    Guid? CompanyId,
    Guid? BranchId,
    Guid? DepartmentId,
    Guid? PositionId,
    Guid? EmployeeTypeId,
    Guid? ProjectId,
    string? Status,
    int Page,
    int PageSize,
    string? Sort);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PersonnelResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static PersonnelResult<T> Success(T value) => new(true, value, null, null);
    public static PersonnelResult<T> Failure(string code, string message) => new(false, null, code, message);
}
