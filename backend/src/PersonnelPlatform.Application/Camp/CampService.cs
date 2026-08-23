using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Camp;

public sealed class CampService(
    ICampRepository repository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<CampResult<IReadOnlyList<CampSiteSummary>>> ListCampsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        return CampResult<IReadOnlyList<CampSiteSummary>>.Success(
            await repository.ListCampsAsync(access.Global, access.CompanyIds, cancellationToken));
    }

    public async Task<CampResult<CampSiteSummary>> CreateCampAsync(Guid userId, CreateCampSiteRequest request, CancellationToken cancellationToken)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, cancellationToken))
            return CampResult<CampSiteSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        try
        {
            var row = CampSite.Create(request.CompanyId, request.Code, request.Name, request.Address, timeProvider.GetUtcNow(), userId);
            repository.AddCamp(row);
            await repository.SaveChangesAsync(cancellationToken);
            return CampResult<CampSiteSummary>.Success(new(row.Id, row.CompanyId, row.Code, row.Name, row.Address, row.IsActive, row.Version));
        }
        catch (ArgumentException)
        {
            return CampResult<CampSiteSummary>.Failure("CAMP_INVALID", "Kamp kodu, adı veya adres bilgisi geçersiz.");
        }
    }

    public async Task<CampResult<IReadOnlyList<CampRoomSummary>>> ListRoomsAsync(Guid userId, Guid campId, CancellationToken cancellationToken)
    {
        var camp = await repository.FindCampAsync(campId, cancellationToken);
        if (camp is null) return CampResult<IReadOnlyList<CampRoomSummary>>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!await HasCompanyScopeAsync(userId, camp.CompanyId, cancellationToken)) return ScopeFailure<IReadOnlyList<CampRoomSummary>>();
        return CampResult<IReadOnlyList<CampRoomSummary>>.Success(await repository.ListRoomsAsync(campId, cancellationToken));
    }

    public async Task<CampResult<CampRoomSummary>> CreateRoomAsync(Guid userId, Guid campId, CreateCampRoomRequest request, CancellationToken cancellationToken)
    {
        var camp = await repository.FindCampAsync(campId, cancellationToken);
        if (camp is null) return CampResult<CampRoomSummary>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!camp.IsActive) return CampResult<CampRoomSummary>.Failure("CAMP_INACTIVE", "Pasif kampa oda eklenemez.");
        if (!await HasCompanyScopeAsync(userId, camp.CompanyId, cancellationToken)) return ScopeFailure<CampRoomSummary>();
        try
        {
            var row = CampRoom.Create(camp.Id, request.Code, request.Name, request.Floor, timeProvider.GetUtcNow(), userId);
            repository.AddRoom(row);
            await repository.SaveChangesAsync(cancellationToken);
            return CampResult<CampRoomSummary>.Success(new(row.Id, row.CampId, row.Code, row.Name, row.Floor, row.IsActive, row.Version));
        }
        catch (ArgumentException)
        {
            return CampResult<CampRoomSummary>.Failure("CAMP_ROOM_INVALID", "Oda bilgileri geçersiz.");
        }
    }

    public async Task<CampResult<IReadOnlyList<CampBedSummary>>> ListBedsAsync(Guid userId, Guid roomId, CancellationToken cancellationToken)
    {
        var room = await repository.FindRoomAsync(roomId, cancellationToken);
        if (room is null) return CampResult<IReadOnlyList<CampBedSummary>>.Failure("CAMP_ROOM_NOT_FOUND", "Oda bulunamadı.");
        var camp = await repository.FindCampAsync(room.CampId, cancellationToken);
        if (camp is null) return CampResult<IReadOnlyList<CampBedSummary>>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!await HasCompanyScopeAsync(userId, camp.CompanyId, cancellationToken)) return ScopeFailure<IReadOnlyList<CampBedSummary>>();
        return CampResult<IReadOnlyList<CampBedSummary>>.Success(await repository.ListBedsAsync(roomId, cancellationToken));
    }

    public async Task<CampResult<CampBedSummary>> CreateBedAsync(Guid userId, Guid roomId, CreateCampBedRequest request, CancellationToken cancellationToken)
    {
        var room = await repository.FindRoomAsync(roomId, cancellationToken);
        if (room is null) return CampResult<CampBedSummary>.Failure("CAMP_ROOM_NOT_FOUND", "Oda bulunamadı.");
        if (!room.IsActive) return CampResult<CampBedSummary>.Failure("CAMP_ROOM_INACTIVE", "Pasif odaya yatak eklenemez.");
        var camp = await repository.FindCampAsync(room.CampId, cancellationToken);
        if (camp is null) return CampResult<CampBedSummary>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!await HasCompanyScopeAsync(userId, camp.CompanyId, cancellationToken)) return ScopeFailure<CampBedSummary>();
        try
        {
            var row = CampBed.Create(room.Id, request.Code, timeProvider.GetUtcNow(), userId);
            repository.AddBed(row);
            await repository.SaveChangesAsync(cancellationToken);
            return CampResult<CampBedSummary>.Success(new(row.Id, row.RoomId, row.Code, row.IsActive, row.Version));
        }
        catch (ArgumentException)
        {
            return CampResult<CampBedSummary>.Failure("CAMP_BED_INVALID", "Yatak bilgileri geçersiz.");
        }
    }

    public async Task<CampResult<IReadOnlyList<AccommodationRateSummary>>> ListRatesAsync(Guid userId, Guid campId, CancellationToken cancellationToken)
    {
        var camp = await repository.FindCampAsync(campId, cancellationToken);
        if (camp is null) return CampResult<IReadOnlyList<AccommodationRateSummary>>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!await HasCompanyScopeAsync(userId, camp.CompanyId, cancellationToken)) return ScopeFailure<IReadOnlyList<AccommodationRateSummary>>();
        return CampResult<IReadOnlyList<AccommodationRateSummary>>.Success(await repository.ListRatesAsync(campId, cancellationToken));
    }

    public async Task<CampResult<AccommodationRateSummary>> CreateRateAsync(Guid userId, Guid campId, CreateAccommodationRateRequest request, CancellationToken cancellationToken)
    {
        var camp = await repository.FindCampAsync(campId, cancellationToken);
        if (camp is null) return CampResult<AccommodationRateSummary>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!await HasCompanyScopeAsync(userId, camp.CompanyId, cancellationToken)) return ScopeFailure<AccommodationRateSummary>();
        try
        {
            var row = AccommodationRate.Create(camp.Id, request.ValidFrom, request.ValidUntilExclusive, request.NightlyRate, request.Currency, timeProvider.GetUtcNow(), userId);
            repository.AddRate(row);
            await repository.SaveChangesAsync(cancellationToken);
            return CampResult<AccommodationRateSummary>.Success(new(row.Id, row.CampId, row.ValidFrom, row.ValidUntilExclusive, row.NightlyRate, row.Currency, row.Version));
        }
        catch (ArgumentException)
        {
            return CampResult<AccommodationRateSummary>.Failure("CAMP_RATE_INVALID", "Konaklama fiyat bilgileri geçersiz.");
        }
    }

    public async Task<CampResult<AccommodationStaySummary>> CreateStayAsync(Guid userId, CreateAccommodationStayRequest request, CancellationToken cancellationToken)
    {
        var employee = await repository.FindEmployeeAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return CampResult<AccommodationStaySummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (employee.Status != EmployeeStatuses.Active) return CampResult<AccommodationStaySummary>.Failure("EMPLOYEE_INACTIVE", "Yalnız aktif personel konaklamaya atanabilir.");
        if (!await HasCompanyScopeAsync(userId, employee.CompanyId, cancellationToken)) return ScopeFailure<AccommodationStaySummary>();

        var camp = await repository.FindCampAsync(request.CampId, cancellationToken);
        if (camp is null) return CampResult<AccommodationStaySummary>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!camp.IsActive) return CampResult<AccommodationStaySummary>.Failure("CAMP_INACTIVE", "Pasif kampa konaklama açılamaz.");
        if (camp.CompanyId != employee.CompanyId) return CampResult<AccommodationStaySummary>.Failure("CAMP_COMPANY_MISMATCH", "Personel ile kamp aynı şirkete bağlı olmalıdır.");

        var room = await repository.FindRoomAsync(request.RoomId, cancellationToken);
        if (room is null || room.CampId != camp.Id) return CampResult<AccommodationStaySummary>.Failure("CAMP_ROOM_MISMATCH", "Oda seçilen kampa bağlı değil.");
        if (!room.IsActive) return CampResult<AccommodationStaySummary>.Failure("CAMP_ROOM_INACTIVE", "Pasif oda konaklamaya atanamaz.");

        var bed = await repository.FindBedAsync(request.BedId, cancellationToken);
        if (bed is null || bed.RoomId != room.Id) return CampResult<AccommodationStaySummary>.Failure("CAMP_BED_MISMATCH", "Yatak seçilen odaya bağlı değil.");
        if (!bed.IsActive) return CampResult<AccommodationStaySummary>.Failure("CAMP_BED_INACTIVE", "Pasif yatak konaklamaya atanamaz.");

        var rate = await repository.FindApplicableRateAsync(camp.Id, request.CheckInDate, cancellationToken);
        if (rate is null) return CampResult<AccommodationStaySummary>.Failure("CAMP_RATE_NOT_FOUND", "Giriş tarihi için geçerli konaklama fiyatı bulunamadı.");

        var project = await repository.FindProjectSnapshotAsync(employee.Id, request.CheckInDate, cancellationToken);
        try
        {
            var row = AccommodationStay.Create(
                employee.CompanyId,
                employee.Id,
                camp.Id,
                room.Id,
                bed.Id,
                rate.Id,
                project?.ProjectId,
                project?.CostCenterId,
                request.CheckInDate,
                request.CheckOutDateExclusive,
                rate.NightlyRate,
                rate.Currency,
                request.Note,
                timeProvider.GetUtcNow(),
                userId);
            repository.AddStay(row);
            await repository.SaveChangesAsync(cancellationToken);
            var summary = await repository.GetStaySummaryAsync(row.Id, TodayExclusive(), cancellationToken);
            return summary is null
                ? CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_SAVE_FAILED", "Konaklama kaydedildi ancak tekrar okunamadı.")
                : CampResult<AccommodationStaySummary>.Success(summary);
        }
        catch (ArgumentException)
        {
            return CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_INVALID", "Konaklama tarihleri veya bilgiler geçersiz.");
        }
    }

    public async Task<CampResult<CampPagedResult<AccommodationStaySummary>>> SearchStaysAsync(Guid userId, AccommodationStayQuery query, CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(userId, cancellationToken);
        var normalized = query with { Page = Math.Max(1, query.Page), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        return CampResult<CampPagedResult<AccommodationStaySummary>>.Success(
            await repository.SearchStaysAsync(normalized, access.Global, access.CompanyIds, TodayExclusive(), cancellationToken));
    }

    public async Task<CampResult<AccommodationStaySummary>> CloseStayAsync(Guid userId, Guid stayId, CloseAccommodationStayRequest request, CancellationToken cancellationToken)
    {
        var stay = await repository.FindStayAsync(stayId, cancellationToken);
        if (stay is null) return CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_NOT_FOUND", "Konaklama bulunamadı.");
        if (!await HasCompanyScopeAsync(userId, stay.CompanyId, cancellationToken)) return ScopeFailure<AccommodationStaySummary>();
        if (stay.Version != request.Version) return CampResult<AccommodationStaySummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Konaklama kaydı başka bir kullanıcı tarafından değiştirildi.");
        try
        {
            stay.Close(request.CheckOutDateExclusive, timeProvider.GetUtcNow(), userId);
            await repository.SaveChangesAsync(cancellationToken);
            var summary = await repository.GetStaySummaryAsync(stay.Id, TodayExclusive(), cancellationToken);
            return summary is null
                ? CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_NOT_FOUND", "Konaklama bulunamadı.")
                : CampResult<AccommodationStaySummary>.Success(summary);
        }
        catch (ArgumentException)
        {
            return CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_CLOSE_DATE_INVALID", "Çıkış tarihi giriş tarihinden sonra olmalıdır.");
        }
        catch (InvalidOperationException)
        {
            return CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_NOT_ACTIVE", "Yalnız aktif konaklama kapatılabilir.");
        }
    }

    public async Task<CampResult<AccommodationStaySummary>> CancelStayAsync(Guid userId, Guid stayId, CancelAccommodationStayRequest request, CancellationToken cancellationToken)
    {
        var stay = await repository.FindStayAsync(stayId, cancellationToken);
        if (stay is null) return CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_NOT_FOUND", "Konaklama bulunamadı.");
        if (!await HasCompanyScopeAsync(userId, stay.CompanyId, cancellationToken)) return ScopeFailure<AccommodationStaySummary>();
        if (stay.Version != request.Version) return CampResult<AccommodationStaySummary>.Failure("RECORD_MODIFIED_BY_ANOTHER_USER", "Konaklama kaydı başka bir kullanıcı tarafından değiştirildi.");
        try
        {
            stay.Cancel(timeProvider.GetUtcNow(), userId);
            await repository.SaveChangesAsync(cancellationToken);
            var summary = await repository.GetStaySummaryAsync(stay.Id, TodayExclusive(), cancellationToken);
            return summary is null
                ? CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_NOT_FOUND", "Konaklama bulunamadı.")
                : CampResult<AccommodationStaySummary>.Success(summary);
        }
        catch (InvalidOperationException)
        {
            return CampResult<AccommodationStaySummary>.Failure("CAMP_STAY_NOT_ACTIVE", "Yalnız aktif konaklama iptal edilebilir.");
        }
    }

    private DateOnly TodayExclusive() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1));

    private Task<bool> HasCompanyScopeAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) =>
        accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, cancellationToken);

    private static CampResult<T> ScopeFailure<T>() where T : class => CampResult<T>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");

    private async Task<CompanyAccess> ResolveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        return new CompanyAccess(
            snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global),
            snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray());
    }

    private sealed record CompanyAccess(bool Global, IReadOnlyCollection<Guid> CompanyIds);
}
