namespace PersonnelPlatform.Application.Camp;

public static class CampPermissions
{
    public const string SiteView = "camp.site.view";
    public const string SiteManage = "camp.site.manage";
    public const string RateView = "camp.rate.view";
    public const string RateManage = "camp.rate.manage";
    public const string StayView = "camp.stay.view";
    public const string StayManage = "camp.stay.manage";
}

public sealed record CampSiteSummary(Guid Id, Guid CompanyId, string Code, string Name, string? Address, bool IsActive, int Version);
public sealed record CreateCampSiteRequest(Guid CompanyId, string Code, string Name, string? Address);

public sealed record CampRoomSummary(Guid Id, Guid CampId, string Code, string Name, int? Floor, bool IsActive, int Version);
public sealed record CreateCampRoomRequest(string Code, string Name, int? Floor);

public sealed record CampBedSummary(Guid Id, Guid RoomId, string Code, bool IsActive, int Version);
public sealed record CreateCampBedRequest(string Code);

public sealed record AccommodationRateSummary(Guid Id, Guid CampId, DateOnly ValidFrom, DateOnly? ValidUntilExclusive, decimal NightlyRate, string Currency, int Version);
public sealed record CreateAccommodationRateRequest(DateOnly ValidFrom, DateOnly? ValidUntilExclusive, decimal NightlyRate, string Currency);

public sealed record CreateAccommodationStayRequest(Guid EmployeeId, Guid CampId, Guid RoomId, Guid BedId, DateOnly CheckInDate, DateOnly? CheckOutDateExclusive, string? Note);
public sealed record CloseAccommodationStayRequest(DateOnly CheckOutDateExclusive, int Version);
public sealed record CancelAccommodationStayRequest(int Version);

public sealed record AccommodationStaySummary(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    Guid CampId,
    string CampCode,
    string CampName,
    Guid RoomId,
    string RoomCode,
    Guid BedId,
    string BedCode,
    Guid RateId,
    Guid? ProjectIdSnapshot,
    Guid? CostCenterIdSnapshot,
    DateOnly CheckInDate,
    DateOnly? CheckOutDateExclusive,
    int Nights,
    decimal NightlyRateSnapshot,
    string CurrencySnapshot,
    decimal CurrentOrFinalCost,
    string Status,
    string? Note,
    int Version);

public sealed record AccommodationStayQuery(Guid? EmployeeId, Guid? CampId, string? Status, DateOnly? From, DateOnly? To, int Page, int PageSize);
public sealed record CampPagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record CampProjectSnapshot(Guid ProjectId, Guid? CostCenterId);

public sealed record CampResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static CampResult<T> Success(T value) => new(true, value, null, null);
    public static CampResult<T> Failure(string code, string message) => new(false, null, code, message);
}
