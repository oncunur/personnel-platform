using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Application.Meal;

public sealed class MealService(
    IMealRepository repository,
    AccessControlService accessControlService,
    TimeProvider timeProvider)
{
    public async Task<MealResult<IReadOnlyList<MealTypeSummary>>> ListMealTypesAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = userId;
        return MealResult<IReadOnlyList<MealTypeSummary>>.Success(await repository.ListMealTypesAsync(cancellationToken));
    }

    public async Task<MealResult<IReadOnlyList<MealRateSummary>>> ListRatesAsync(Guid userId, Guid campId, Guid? mealTypeId, CancellationToken cancellationToken)
    {
        var camp = await repository.FindCampAsync(campId, cancellationToken);
        if (camp is null) return MealResult<IReadOnlyList<MealRateSummary>>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, camp.CompanyId, cancellationToken))
            return MealResult<IReadOnlyList<MealRateSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return MealResult<IReadOnlyList<MealRateSummary>>.Success(await repository.ListRatesAsync(campId, mealTypeId, cancellationToken));
    }

    public async Task<MealResult<MealRateSummary>> CreateRateAsync(Guid userId, CreateMealRateRequest request, CancellationToken cancellationToken)
    {
        var camp = await repository.FindCampAsync(request.CampId, cancellationToken);
        if (camp is null) return MealResult<MealRateSummary>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!camp.IsActive) return MealResult<MealRateSummary>.Failure("CAMP_INACTIVE", "Pasif kamp için yemek fiyatı tanımlanamaz.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, camp.CompanyId, cancellationToken))
            return MealResult<MealRateSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var mealType = await repository.FindMealTypeAsync(request.MealTypeId, cancellationToken);
        if (mealType is null || !mealType.IsActive) return MealResult<MealRateSummary>.Failure("MEAL_TYPE_NOT_FOUND", "Aktif öğün türü bulunamadı.");

        try
        {
            var row = MealRate.Create(request.CampId, request.MealTypeId, request.ValidFrom, request.ValidUntilExclusive, request.UnitPrice, request.Currency, timeProvider.GetUtcNow(), userId);
            repository.AddRate(row);
            await repository.SaveChangesAsync(cancellationToken);
            return MealResult<MealRateSummary>.Success(new(row.Id, row.CampId, row.MealTypeId, mealType.Code, mealType.Name, row.ValidFrom, row.ValidUntilExclusive, row.UnitPrice, row.Currency, row.Version));
        }
        catch (ArgumentException)
        {
            return MealResult<MealRateSummary>.Failure("MEAL_RATE_INVALID", "Yemek fiyat bilgileri geçersiz.");
        }
    }

    public async Task<MealResult<MealConsumptionSummary>> RecordConsumptionAsync(Guid userId, CreateMealConsumptionRequest request, CancellationToken cancellationToken)
    {
        var employee = await repository.FindEmployeeAsync(request.EmployeeId, cancellationToken);
        if (employee is null) return MealResult<MealConsumptionSummary>.Failure("EMPLOYEE_NOT_FOUND", "Personel bulunamadı.");
        if (employee.Status != EmployeeStatuses.Active) return MealResult<MealConsumptionSummary>.Failure("EMPLOYEE_INACTIVE", "Yalnız aktif personel için yemek tüketimi kaydedilebilir.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, employee.CompanyId, cancellationToken))
            return MealResult<MealConsumptionSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");

        var camp = await repository.FindCampAsync(request.CampId, cancellationToken);
        if (camp is null) return MealResult<MealConsumptionSummary>.Failure("CAMP_NOT_FOUND", "Kamp bulunamadı.");
        if (!camp.IsActive) return MealResult<MealConsumptionSummary>.Failure("CAMP_INACTIVE", "Pasif kampta yemek tüketimi kaydedilemez.");
        if (camp.CompanyId != employee.CompanyId) return MealResult<MealConsumptionSummary>.Failure("CAMP_COMPANY_MISMATCH", "Personel ile kamp aynı şirkete bağlı olmalıdır.");

        var mealType = await repository.FindMealTypeAsync(request.MealTypeId, cancellationToken);
        if (mealType is null || !mealType.IsActive) return MealResult<MealConsumptionSummary>.Failure("MEAL_TYPE_NOT_FOUND", "Aktif öğün türü bulunamadı.");

        if (await repository.FindDuplicateConsumptionAsync(employee.Id, request.ConsumptionDate, request.MealTypeId, cancellationToken) is not null)
            return MealResult<MealConsumptionSummary>.Failure("MEAL_ALREADY_CONSUMED", "Personel için bu gün ve öğün türünde tüketim kaydı zaten bulunuyor.");

        var normalizedSource = string.IsNullOrWhiteSpace(request.Source) ? MealConsumptionSources.Manual : request.Source.Trim().ToUpperInvariant();
        if (normalizedSource != MealConsumptionSources.Manual && !string.IsNullOrWhiteSpace(request.ExternalEventId))
        {
            var existingExternal = await repository.FindByExternalEventAsync(employee.CompanyId, normalizedSource, request.ExternalEventId.Trim(), cancellationToken);
            if (existingExternal is not null)
                return MealResult<MealConsumptionSummary>.Failure("MEAL_EXTERNAL_EVENT_DUPLICATE", "Aynı harici yemek olayı daha önce kaydedilmiş.");
        }

        var rate = await repository.FindApplicableRateAsync(camp.Id, mealType.Id, request.ConsumptionDate, cancellationToken);
        if (rate is null) return MealResult<MealConsumptionSummary>.Failure("MEAL_RATE_NOT_FOUND", "Bu tarih, kamp ve öğün türü için geçerli fiyat bulunamadı.");
        var project = await repository.FindProjectSnapshotAsync(employee.Id, request.ConsumptionDate, cancellationToken);

        try
        {
            var row = MealConsumption.Create(
                employee.CompanyId,
                employee.Id,
                camp.Id,
                mealType.Id,
                rate.Id,
                request.ConsumptionDate,
                request.Quantity,
                rate.UnitPrice,
                rate.Currency,
                project?.ProjectId,
                project?.CostCenterId,
                normalizedSource,
                request.ExternalEventId,
                request.Note,
                timeProvider.GetUtcNow(),
                userId);
            repository.AddConsumption(row);
            await repository.SaveChangesAsync(cancellationToken);
            var summary = await repository.GetConsumptionSummaryAsync(row.Id, cancellationToken);
            return summary is null
                ? MealResult<MealConsumptionSummary>.Failure("MEAL_CONSUMPTION_SAVE_FAILED", "Yemek tüketimi kaydedildi ancak tekrar okunamadı.")
                : MealResult<MealConsumptionSummary>.Success(summary);
        }
        catch (ArgumentException)
        {
            return MealResult<MealConsumptionSummary>.Failure("MEAL_CONSUMPTION_INVALID", "Yemek tüketim bilgileri geçersiz.");
        }
    }

    public async Task<MealResult<MealPagedResult<MealConsumptionSummary>>> SearchConsumptionsAsync(Guid userId, MealConsumptionQuery query, CancellationToken cancellationToken)
    {
        var snapshot = await accessControlService.GetSnapshotAsync(userId, cancellationToken);
        var global = snapshot.Scopes.Any(x => x.ScopeType == ScopeTypes.Global);
        var companies = snapshot.Scopes.Where(x => x.ScopeType == ScopeTypes.Company && x.ScopeId is not null).Select(x => x.ScopeId!.Value).Distinct().ToArray();
        var normalized = query with { Page = Math.Max(1, query.Page), PageSize = Math.Clamp(query.PageSize, 1, 100) };
        return MealResult<MealPagedResult<MealConsumptionSummary>>.Success(
            await repository.SearchConsumptionsAsync(normalized, global, companies, cancellationToken));
    }
}
