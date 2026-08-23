using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Camp;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Camp;

public sealed class CampRepository(ApplicationDbContext dbContext) : ICampRepository
{
    public Task<CampSite?> FindCampAsync(Guid campId, CancellationToken cancellationToken) =>
        dbContext.Camps.FirstOrDefaultAsync(x => x.Id == campId && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<CampSiteSummary>> ListCampsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
    {
        var query = dbContext.Camps.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        return await query.OrderBy(x => x.Name)
            .Select(x => new CampSiteSummary(x.Id, x.CompanyId, x.Code, x.Name, x.Address, x.IsActive, x.Version))
            .ToListAsync(cancellationToken);
    }

    public void AddCamp(CampSite camp) => dbContext.Camps.Add(camp);

    public Task<CampRoom?> FindRoomAsync(Guid roomId, CancellationToken cancellationToken) =>
        dbContext.CampRooms.FirstOrDefaultAsync(x => x.Id == roomId && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<CampRoomSummary>> ListRoomsAsync(Guid campId, CancellationToken cancellationToken) =>
        await dbContext.CampRooms.AsNoTracking().Where(x => x.CampId == campId && x.DeletedAt == null)
            .OrderBy(x => x.Code)
            .Select(x => new CampRoomSummary(x.Id, x.CampId, x.Code, x.Name, x.Floor, x.IsActive, x.Version))
            .ToListAsync(cancellationToken);

    public void AddRoom(CampRoom room) => dbContext.CampRooms.Add(room);

    public Task<CampBed?> FindBedAsync(Guid bedId, CancellationToken cancellationToken) =>
        dbContext.CampBeds.FirstOrDefaultAsync(x => x.Id == bedId && x.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<CampBedSummary>> ListBedsAsync(Guid roomId, CancellationToken cancellationToken) =>
        await dbContext.CampBeds.AsNoTracking().Where(x => x.RoomId == roomId && x.DeletedAt == null)
            .OrderBy(x => x.Code)
            .Select(x => new CampBedSummary(x.Id, x.RoomId, x.Code, x.IsActive, x.Version))
            .ToListAsync(cancellationToken);

    public void AddBed(CampBed bed) => dbContext.CampBeds.Add(bed);

    public Task<AccommodationRate?> FindRateAsync(Guid rateId, CancellationToken cancellationToken) =>
        dbContext.AccommodationRates.FirstOrDefaultAsync(x => x.Id == rateId && x.DeletedAt == null, cancellationToken);

    public Task<AccommodationRate?> FindApplicableRateAsync(Guid campId, DateOnly date, CancellationToken cancellationToken) =>
        dbContext.AccommodationRates.AsNoTracking().FirstOrDefaultAsync(
            x => x.CampId == campId && x.DeletedAt == null && x.ValidFrom <= date && (x.ValidUntilExclusive == null || date < x.ValidUntilExclusive),
            cancellationToken);

    public async Task<IReadOnlyList<AccommodationRateSummary>> ListRatesAsync(Guid campId, CancellationToken cancellationToken) =>
        await dbContext.AccommodationRates.AsNoTracking().Where(x => x.CampId == campId && x.DeletedAt == null)
            .OrderByDescending(x => x.ValidFrom)
            .Select(x => new AccommodationRateSummary(x.Id, x.CampId, x.ValidFrom, x.ValidUntilExclusive, x.NightlyRate, x.Currency, x.Version))
            .ToListAsync(cancellationToken);

    public void AddRate(AccommodationRate rate) => dbContext.AccommodationRates.Add(rate);

    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken) =>
        dbContext.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && x.DeletedAt == null, cancellationToken);

    public Task<CampProjectSnapshot?> FindProjectSnapshotAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken) =>
        dbContext.EmployeeProjectAssignments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId
                        && x.DeletedAt == null
                        && x.Status == ProjectAssignmentStatuses.Active
                        && x.ValidFrom <= date
                        && (x.ValidUntil == null || x.ValidUntil >= date))
            .OrderByDescending(x => x.AllocationPercent)
            .ThenByDescending(x => x.ValidFrom)
            .Select(x => new CampProjectSnapshot(x.ProjectId, x.CostCenterId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<AccommodationStay?> FindStayAsync(Guid stayId, CancellationToken cancellationToken) =>
        dbContext.AccommodationStays.FirstOrDefaultAsync(x => x.Id == stayId && x.DeletedAt == null, cancellationToken);

    public async Task<AccommodationStaySummary?> GetStaySummaryAsync(Guid stayId, DateOnly asOfExclusive, CancellationToken cancellationToken)
    {
        var row = await StayQuery().FirstOrDefaultAsync(x => x.Stay.Id == stayId, cancellationToken);
        return row is null ? null : ToSummary(row, asOfExclusive);
    }

    public async Task<CampPagedResult<AccommodationStaySummary>> SearchStaysAsync(AccommodationStayQuery query, bool globalAccess, IReadOnlyCollection<Guid> companyIds, DateOnly asOfExclusive, CancellationToken cancellationToken)
    {
        var source = StayQuery();
        if (!globalAccess) source = source.Where(x => companyIds.Contains(x.Stay.CompanyId));
        if (query.EmployeeId is not null) source = source.Where(x => x.Stay.EmployeeId == query.EmployeeId.Value);
        if (query.CampId is not null) source = source.Where(x => x.Stay.CampId == query.CampId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            source = source.Where(x => x.Stay.Status == status);
        }
        if (query.From is not null) source = source.Where(x => (x.Stay.CheckOutDateExclusive ?? DateOnly.MaxValue) > query.From.Value);
        if (query.To is not null) source = source.Where(x => x.Stay.CheckInDate < query.To.Value.AddDays(1));

        var total = await source.CountAsync(cancellationToken);
        var rows = await source.OrderByDescending(x => x.Stay.CheckInDate)
            .ThenBy(x => x.Employee.EmployeeNo)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new CampPagedResult<AccommodationStaySummary>(rows.Select(x => ToSummary(x, asOfExclusive)).ToArray(), query.Page, query.PageSize, total);
    }

    public void AddStay(AccommodationStay stay) => dbContext.AccommodationStays.Add(stay);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<StayJoin> StayQuery() =>
        from stay in dbContext.AccommodationStays.AsNoTracking()
        join employee in dbContext.Employees.AsNoTracking() on stay.EmployeeId equals employee.Id
        join camp in dbContext.Camps.AsNoTracking() on stay.CampId equals camp.Id
        join room in dbContext.CampRooms.AsNoTracking() on stay.RoomId equals room.Id
        join bed in dbContext.CampBeds.AsNoTracking() on stay.BedId equals bed.Id
        where stay.DeletedAt == null && employee.DeletedAt == null && camp.DeletedAt == null && room.DeletedAt == null && bed.DeletedAt == null
        select new StayJoin(stay, employee, camp, room, bed);

    private static AccommodationStaySummary ToSummary(StayJoin row, DateOnly asOfExclusive)
    {
        var nights = row.Stay.Status == AccommodationStayStatuses.Cancelled ? 0 : row.Stay.NightsAsOf(asOfExclusive);
        var cost = row.Stay.Status switch
        {
            AccommodationStayStatuses.Closed => row.Stay.TotalCostSnapshot,
            AccommodationStayStatuses.Cancelled => 0m,
            _ => row.Stay.CostAsOf(asOfExclusive)
        };
        return new AccommodationStaySummary(
            row.Stay.Id,
            row.Stay.CompanyId,
            row.Stay.EmployeeId,
            row.Employee.EmployeeNo,
            row.Employee.FirstName + " " + row.Employee.LastName,
            row.Stay.CampId,
            row.Camp.Code,
            row.Camp.Name,
            row.Stay.RoomId,
            row.Room.Code,
            row.Stay.BedId,
            row.Bed.Code,
            row.Stay.RateId,
            row.Stay.ProjectIdSnapshot,
            row.Stay.CostCenterIdSnapshot,
            row.Stay.CheckInDate,
            row.Stay.CheckOutDateExclusive,
            nights,
            row.Stay.NightlyRateSnapshot,
            row.Stay.CurrencySnapshot,
            cost,
            row.Stay.Status,
            row.Stay.Note,
            row.Stay.Version);
    }

    private sealed record StayJoin(AccommodationStay Stay, Employee Employee, CampSite Camp, CampRoom Room, CampBed Bed);
}
