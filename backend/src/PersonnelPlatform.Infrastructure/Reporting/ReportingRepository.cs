using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Application.Reporting;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Domain.Reporting;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Reporting;

public sealed class ReportingRepository(ApplicationDbContext db) : IReportingRepository
{
    public async Task<Project360Summary?> GetProject360Async(Guid projectId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId && x.DeletedAt == null, ct);
        if (project is null) return null;

        var targetAssignments = await db.EmployeeProjectAssignments.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.ProjectId == projectId && x.ValidFrom <= to && (x.ValidUntil == null || x.ValidUntil >= from))
            .ToListAsync(ct);
        var employeeIds = targetAssignments.Select(x => x.EmployeeId).Distinct().ToArray();

        decimal manDays = 0m;
        decimal workedMinutes = 0m;
        decimal overtimeMinutes = 0m;
        if (employeeIds.Length > 0)
        {
            var allAssignments = await db.EmployeeProjectAssignments.AsNoTracking()
                .Where(x => x.DeletedAt == null && employeeIds.Contains(x.EmployeeId) && x.ValidFrom <= to && (x.ValidUntil == null || x.ValidUntil >= from))
                .ToListAsync(ct);

            var attendance = await db.DailyAttendances.AsNoTracking()
                .Where(x => x.DeletedAt == null && employeeIds.Contains(x.EmployeeId) && x.AttendanceDate >= from && x.AttendanceDate <= to)
                .ToListAsync(ct);
            foreach (var day in attendance)
            {
                var share = ProjectShare(day.EmployeeId, day.AttendanceDate, projectId, allAssignments);
                if (share <= 0) continue;
                if (day.WorkedMinutes + day.LeaveMinutes > 0) manDays += share;
                workedMinutes += day.WorkedMinutes * share;
            }

            var overtime = await db.OvertimeRequests.AsNoTracking()
                .Where(x => x.DeletedAt == null && employeeIds.Contains(x.EmployeeId) && x.Status == OvertimeRequestStatuses.Approved && x.AttendanceDate >= from && x.AttendanceDate <= to)
                .ToListAsync(ct);
            foreach (var row in overtime)
            {
                var share = ProjectShare(row.EmployeeId, row.AttendanceDate, projectId, allAssignments);
                if (share > 0) overtimeMinutes += row.ApprovedMinutes * share;
            }
        }

        var mealRows = await db.MealConsumptions.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.ProjectIdSnapshot == projectId && x.ConsumptionDate >= from && x.ConsumptionDate <= to)
            .ToListAsync(ct);
        var mealQuantity = mealRows.Sum(x => x.Quantity);

        var stayRows = await db.AccommodationStays.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.ProjectIdSnapshot == projectId && x.Status != AccommodationStayStatuses.Cancelled && x.CheckInDate <= to && (x.CheckOutDateExclusive == null || x.CheckOutDateExclusive > from))
            .ToListAsync(ct);
        var reportEndExclusive = to.AddDays(1);
        var nights = stayRows.Sum(x => OverlapDays(x.CheckInDate, x.CheckOutDateExclusive ?? reportEndExclusive, from, reportEndExclusive));

        var costs = await db.CostEntries.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.CostDate >= from && x.CostDate <= to)
            .GroupBy(x => x.Currency)
            .Select(g => new CurrencyCostSummary(
                g.Key,
                g.Where(x => x.Category == CostCategories.Payroll).Sum(x => x.Amount),
                g.Where(x => x.Category == CostCategories.Meal).Sum(x => x.Amount),
                g.Where(x => x.Category == CostCategories.Accommodation).Sum(x => x.Amount),
                g.Sum(x => x.Amount)))
            .OrderBy(x => x.Currency)
            .ToListAsync(ct);

        return new Project360Summary(
            project.Id,
            project.CompanyId,
            project.Code,
            project.Name,
            from,
            to,
            employeeIds.Length,
            decimal.Round(manDays, 4, MidpointRounding.AwayFromZero),
            decimal.Round(workedMinutes / 60m, 2, MidpointRounding.AwayFromZero),
            decimal.Round(overtimeMinutes / 60m, 2, MidpointRounding.AwayFromZero),
            decimal.Round(mealQuantity, 2, MidpointRounding.AwayFromZero),
            nights,
            costs);
    }

    public async Task<IReadOnlyList<ManagementProjectSummary>> ListManagementAsync(Guid companyId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var projectIds = await db.Projects.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DeletedAt == null)
            .OrderBy(x => x.Code)
            .Select(x => x.Id)
            .ToListAsync(ct);
        var result = new List<ManagementProjectSummary>(projectIds.Count);
        foreach (var projectId in projectIds)
        {
            var p = await GetProject360Async(projectId, from, to, ct);
            if (p is null) continue;
            result.Add(new ManagementProjectSummary(p.ProjectId, p.ProjectCode, p.ProjectName, p.Headcount, p.ManDays, p.WorkedHours, p.ApprovedOvertimeHours, p.MealQuantity, p.AccommodationNights, p.Costs));
        }
        return result;
    }

    public void AddExportJob(ReportExportJob job) => db.ReportExportJobs.Add(job);

    public Task<ReportExportJob?> FindExportJobAsync(Guid exportJobId, CancellationToken ct) =>
        db.ReportExportJobs.FirstOrDefaultAsync(x => x.Id == exportJobId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<ReportExportJobSummary>> ListExportJobsAsync(Guid userId, Guid? companyId, int take, CancellationToken ct)
    {
        var q = db.ReportExportJobs.AsNoTracking().Where(x => x.RequestedByUserId == userId && x.DeletedAt == null);
        if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value);
        return await q.OrderByDescending(x => x.CreatedAt).Take(take)
            .Select(x => new ReportExportJobSummary(x.Id, x.CompanyId, x.RequestedByUserId, x.ReportType, x.Format, x.FiltersJson, x.Status, x.FileName, x.ContentType, x.FileSizeBytes, x.StartedAt, x.CompletedAt, x.ErrorMessage, x.CreatedAt, x.Version))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReportExportJob>> ListQueuedExportJobsAsync(int take, CancellationToken ct) =>
        await db.ReportExportJobs.Where(x => x.DeletedAt == null && x.Status == ReportExportStatuses.Queued).OrderBy(x => x.CreatedAt).Take(take).ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static decimal ProjectShare(Guid employeeId, DateOnly date, Guid projectId, IReadOnlyList<EmployeeProjectAssignment> assignments)
    {
        var active = assignments.Where(x => x.EmployeeId == employeeId && x.ValidFrom <= date && (x.ValidUntil == null || x.ValidUntil >= date)).ToArray();
        var total = active.Sum(x => x.AllocationPercent);
        if (total <= 0) return 0m;
        var target = active.Where(x => x.ProjectId == projectId).Sum(x => x.AllocationPercent);
        return target <= 0 ? 0m : target / total;
    }

    private static int OverlapDays(DateOnly start, DateOnly endExclusive, DateOnly rangeStart, DateOnly rangeEndExclusive)
    {
        var effectiveStart = start > rangeStart ? start : rangeStart;
        var effectiveEnd = endExclusive < rangeEndExclusive ? endExclusive : rangeEndExclusive;
        return Math.Max(0, effectiveEnd.DayNumber - effectiveStart.DayNumber);
    }
}
