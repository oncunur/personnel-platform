using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Personnel;

public interface IEmployeeSensitiveRepository
{
    Task<EmployeeSensitiveProfile?> FindAsync(Guid employeeId, CancellationToken cancellationToken);
    void Add(EmployeeSensitiveProfile profile);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
