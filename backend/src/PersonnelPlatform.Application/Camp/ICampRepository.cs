using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Camp;

public interface ICampRepository
{
    Task<CampSite?> FindCampAsync(Guid campId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CampSiteSummary>> ListCampsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken);
    void AddCamp(CampSite camp);

    Task<CampRoom?> FindRoomAsync(Guid roomId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CampRoomSummary>> ListRoomsAsync(Guid campId, CancellationToken cancellationToken);
    void AddRoom(CampRoom room);

    Task<CampBed?> FindBedAsync(Guid bedId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CampBedSummary>> ListBedsAsync(Guid roomId, CancellationToken cancellationToken);
    void AddBed(CampBed bed);

    Task<AccommodationRate?> FindRateAsync(Guid rateId, CancellationToken cancellationToken);
    Task<AccommodationRate?> FindApplicableRateAsync(Guid campId, DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccommodationRateSummary>> ListRatesAsync(Guid campId, CancellationToken cancellationToken);
    void AddRate(AccommodationRate rate);

    Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<CampProjectSnapshot?> FindProjectSnapshotAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken);

    Task<AccommodationStay?> FindStayAsync(Guid stayId, CancellationToken cancellationToken);
    Task<AccommodationStaySummary?> GetStaySummaryAsync(Guid stayId, DateOnly asOfExclusive, CancellationToken cancellationToken);
    Task<CampPagedResult<AccommodationStaySummary>> SearchStaysAsync(AccommodationStayQuery query, bool globalAccess, IReadOnlyCollection<Guid> companyIds, DateOnly asOfExclusive, CancellationToken cancellationToken);
    void AddStay(AccommodationStay stay);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
