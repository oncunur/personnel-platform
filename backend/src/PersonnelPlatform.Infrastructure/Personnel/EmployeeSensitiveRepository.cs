using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Personnel;

public sealed class EmployeeSensitiveRepository(ApplicationDbContext db) : IEmployeeSensitiveRepository
{
    public Task<EmployeeSensitiveProfile?> FindAsync(Guid employeeId, CancellationToken cancellationToken) =>
        db.EmployeeSensitiveProfiles.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.DeletedAt == null, cancellationToken);
    public void Add(EmployeeSensitiveProfile profile) => db.EmployeeSensitiveProfiles.Add(profile);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
