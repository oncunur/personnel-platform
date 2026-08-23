using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Administration;

public sealed class AdministrativeAffairsRepository(ApplicationDbContext db) : IAdministrativeAffairsRepository
{
    public Task<User?> FindUserAsync(Guid userId, CancellationToken ct) => db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.DeletedAt == null, ct);

    public Task<AdministrativeTask?> FindTaskAsync(Guid taskId, CancellationToken ct) => db.AdministrativeTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<AdministrativeTaskSummary>> ListTasksAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? responsibleUserId, string? status, CancellationToken ct)
    {
        var query = db.AdministrativeTasks.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        if (responsibleUserId is not null) query = query.Where(x => x.ResponsibleUserId == responsibleUserId.Value);
        if (status is not null) query = query.Where(x => x.Status == status);
        return await (from t in query
                      join u in db.Users.AsNoTracking() on t.ResponsibleUserId equals u.Id
                      orderby t.DueDate, t.Code
                      select new AdministrativeTaskSummary(t.Id, t.CompanyId, t.Code, t.Title, t.Description, t.ResponsibleUserId, u.Username, t.DueDate, t.RecurrenceUnit, t.RecurrenceInterval, t.ReminderDaysBefore, t.Status, t.CompletionCount, t.LastCompletedAt, t.Version)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdministrativeTaskCompletionSummary>> ListTaskCompletionsAsync(Guid taskId, int take, CancellationToken ct) =>
        await (from c in db.AdministrativeTaskCompletions.AsNoTracking()
               join u in db.Users.AsNoTracking() on c.CompletedBy equals u.Id
               where c.TaskId == taskId
               orderby c.CompletedAt descending
               select new AdministrativeTaskCompletionSummary(c.Id, c.TaskId, c.DueDateSnapshot, c.CompletedLocalDate, c.CompletedAt, c.CompletedBy, u.Username, c.Note)).Take(take).ToListAsync(ct);

    public void AddTask(AdministrativeTask task) => db.AdministrativeTasks.Add(task);
    public void AddTaskCompletion(AdministrativeTaskCompletion completion) => db.AdministrativeTaskCompletions.Add(completion);

    public Task<AdministrativeContract?> FindContractAsync(Guid contractId, CancellationToken ct) => db.AdministrativeContracts.FirstOrDefaultAsync(x => x.Id == contractId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<AdministrativeContractSummary>> ListContractsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? responsibleUserId, string? status, DateOnly today, CancellationToken ct)
    {
        var query = db.AdministrativeContracts.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        if (responsibleUserId is not null) query = query.Where(x => x.ResponsibleUserId == responsibleUserId.Value);
        var rows = await (from c in query join u in db.Users.AsNoTracking() on c.ResponsibleUserId equals u.Id orderby c.EndDate, c.ContractNo select new { c, u.Username }).ToListAsync(ct);
        var summaries = rows.Select(x =>
        {
            var effective = x.c.Status == AdministrativeContractStatuses.Closed ? "CLOSED" : today > x.c.EndDate ? "EXPIRED" : today >= x.c.EndDate.AddDays(-x.c.ReminderDaysBefore) ? "EXPIRING" : "ACTIVE";
            return new AdministrativeContractSummary(x.c.Id, x.c.CompanyId, x.c.ContractNo, x.c.Title, x.c.Counterparty, x.c.ResponsibleUserId, x.Username, x.c.StartDate, x.c.EndDate, x.c.ReminderDaysBefore, x.c.AutoRenewal, x.c.ContractValue, x.c.Currency, x.c.Status, effective, x.c.Note, x.c.Version);
        });
        if (status is not null) summaries = summaries.Where(x => x.EffectiveStatus == status || x.StoredStatus == status);
        return summaries.ToArray();
    }

    public void AddContract(AdministrativeContract contract) => db.AdministrativeContracts.Add(contract);

    public async Task<IReadOnlyList<AdministrativeReminderCandidate>> BuildReminderCandidatesAsync(DateOnly today, int vehicleDateHorizonDays, int taskDefaultHorizonDays, int maintenanceKmThreshold, CancellationToken ct)
    {
        var candidates = new List<AdministrativeReminderCandidate>();
        var vehicles = await db.Vehicles.AsNoTracking().Where(x => x.DeletedAt == null && x.Status != VehicleStatuses.Retired).ToListAsync(ct);
        foreach (var v in vehicles)
        {
            AddVehicleDateCandidate(candidates, v.CompanyId, v.Id, v.Plate, "VEHICLE_INSURANCE_DUE", "Sigorta", v.InsuranceValidUntil, today, vehicleDateHorizonDays);
            AddVehicleDateCandidate(candidates, v.CompanyId, v.Id, v.Plate, "VEHICLE_INSPECTION_DUE", "Muayene", v.InspectionValidUntil, today, vehicleDateHorizonDays);
        }

        var vehicleIds = vehicles.Select(x => x.Id).ToArray();
        var currentKm = await db.VehicleOdometerEvents.AsNoTracking().Where(x => vehicleIds.Contains(x.VehicleId) && x.DeletedAt == null)
            .GroupBy(x => x.VehicleId).Select(g => new { VehicleId = g.Key, Km = g.Max(x => x.OdometerKm) }).ToDictionaryAsync(x => x.VehicleId, x => x.Km, ct);
        var maintenance = await (from m in db.VehicleMaintenanceRecords.AsNoTracking()
                                 join v in db.Vehicles.AsNoTracking() on m.VehicleId equals v.Id
                                 where m.DeletedAt == null && v.DeletedAt == null && v.Status != VehicleStatuses.Retired && (m.NextDueDate != null || m.NextDueOdometerKm != null)
                                 select new { m, v.Plate }).ToListAsync(ct);
        foreach (var x in maintenance)
        {
            if (x.m.NextDueDate is not null && today >= x.m.NextDueDate.Value.AddDays(-vehicleDateHorizonDays))
            {
                var due = x.m.NextDueDate.Value;
                candidates.Add(new(x.m.CompanyId, "VEHICLE_MAINTENANCE_DATE_DUE", "VEHICLE_MAINTENANCE", x.m.Id, due, Severity(today, due), $"VEHICLE_MAINT_DATE:{x.m.Id:N}:{due:yyyyMMdd}", $"{x.Plate} için {x.m.MaintenanceType} bakım tarihi yaklaşıyor: {due:yyyy-MM-dd}.", JsonSerializer.Serialize(new { x.m.VehicleId, x.m.MaintenanceType, dueDate = due })));
            }
            if (x.m.NextDueOdometerKm is not null && currentKm.TryGetValue(x.m.VehicleId, out var km) && km >= x.m.NextDueOdometerKm.Value - maintenanceKmThreshold)
            {
                var dueKm = x.m.NextDueOdometerKm.Value;
                var severity = km >= dueKm ? "CRITICAL" : dueKm - km <= 250 ? "IMPORTANT" : "NORMAL";
                candidates.Add(new(x.m.CompanyId, "VEHICLE_MAINTENANCE_KM_DUE", "VEHICLE_MAINTENANCE", x.m.Id, null, severity, $"VEHICLE_MAINT_KM:{x.m.Id:N}:{dueKm}", $"{x.Plate} için {x.m.MaintenanceType} bakım kilometresi yaklaşıyor: {km}/{dueKm} km.", JsonSerializer.Serialize(new { x.m.VehicleId, x.m.MaintenanceType, currentKm = km, dueKm })));
            }
        }

        var tasks = await db.AdministrativeTasks.AsNoTracking().Where(x => x.DeletedAt == null && x.Status == AdministrativeTaskStatuses.Open).ToListAsync(ct);
        foreach (var task in tasks)
        {
            var horizon = task.ReminderDaysBefore > 0 ? task.ReminderDaysBefore : taskDefaultHorizonDays;
            if (today < task.DueDate.AddDays(-horizon)) continue;
            candidates.Add(new(task.CompanyId, "ADMIN_TASK_DUE", "ADMIN_TASK", task.Id, task.DueDate, Severity(today, task.DueDate), $"ADMIN_TASK:{task.Id:N}:{task.DueDate:yyyyMMdd}", $"İdari görev son tarihi yaklaşıyor: {task.Code} · {task.Title} · {task.DueDate:yyyy-MM-dd}.", JsonSerializer.Serialize(new { task.Code, task.Title, task.ResponsibleUserId, task.DueDate })));
        }

        var contracts = await db.AdministrativeContracts.AsNoTracking().Where(x => x.DeletedAt == null && x.Status == AdministrativeContractStatuses.Active).ToListAsync(ct);
        foreach (var contract in contracts)
        {
            if (today < contract.EndDate.AddDays(-contract.ReminderDaysBefore)) continue;
            candidates.Add(new(contract.CompanyId, "ADMIN_CONTRACT_EXPIRY_DUE", "ADMIN_CONTRACT", contract.Id, contract.EndDate, Severity(today, contract.EndDate), $"ADMIN_CONTRACT:{contract.Id:N}:{contract.EndDate:yyyyMMdd}", $"Kontrat bitiş tarihi yaklaşıyor: {contract.ContractNo} · {contract.Title} · {contract.EndDate:yyyy-MM-dd}.", JsonSerializer.Serialize(new { contract.ContractNo, contract.Title, contract.Counterparty, contract.ResponsibleUserId, contract.EndDate, contract.AutoRenewal })));
        }
        return candidates;
    }

    public async Task<bool> TryInsertReminderAsync(AdministrativeReminderCandidate c, DateTimeOffset createdAt, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO administration.reminder_events
                (id, company_id, event_type, source_type, source_id, due_date, severity, dedupe_key, message, metadata_json, created_at)
            VALUES ({id}, {c.CompanyId}, {c.EventType}, {c.SourceType}, {c.SourceId}, {c.DueDate}, {c.Severity}, {c.DedupeKey}, {c.Message}, CAST({c.MetadataJson} AS jsonb), {createdAt.ToUniversalTime()})
            ON CONFLICT (dedupe_key) DO NOTHING
            """, ct);
        return affected > 0;
    }

    public async Task<IReadOnlyList<AdministrativeReminderSummary>> ListRemindersAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, string? eventType, DateTimeOffset? from, int take, CancellationToken ct)
    {
        var query = db.AdministrativeReminderEvents.AsNoTracking().AsQueryable();
        if (!globalAccess) query = query.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) query = query.Where(x => x.CompanyId == companyId.Value);
        if (eventType is not null) query = query.Where(x => x.EventType == eventType);
        if (from is not null) query = query.Where(x => x.CreatedAt >= from.Value);
        return await query.OrderByDescending(x => x.CreatedAt).Take(take).Select(x => new AdministrativeReminderSummary(x.Id, x.CompanyId, x.EventType, x.SourceType, x.SourceId, x.DueDate, x.Severity, x.Message, x.MetadataJson, x.CreatedAt)).ToListAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static void AddVehicleDateCandidate(List<AdministrativeReminderCandidate> target, Guid companyId, Guid vehicleId, string plate, string type, string label, DateOnly? due, DateOnly today, int horizon)
    {
        if (due is null || today < due.Value.AddDays(-horizon)) return;
        target.Add(new(companyId, type, "VEHICLE", vehicleId, due, Severity(today, due.Value), $"{type}:{vehicleId:N}:{due.Value:yyyyMMdd}", $"{plate} için {label.ToLowerInvariant()} tarihi yaklaşıyor: {due.Value:yyyy-MM-dd}.", JsonSerializer.Serialize(new { vehicleId, plate, dueDate = due.Value })));
    }

    private static string Severity(DateOnly today, DateOnly due) => today > due ? "CRITICAL" : due.DayNumber - today.DayNumber <= 7 ? "IMPORTANT" : "NORMAL";
}
