using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonnelPlatform.Application.Workflow;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Personnel;
using PersonnelPlatform.Domain.Workflow;
using PersonnelPlatform.Infrastructure.Persistence;

namespace PersonnelPlatform.Infrastructure.Workflow;

public sealed class WorkflowRepository(ApplicationDbContext db) : IWorkflowRepository
{
    public Task<User?> FindUserAsync(Guid userId, CancellationToken ct) => db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.DeletedAt == null, ct);
    public Task<Role?> FindRoleAsync(Guid roleId, CancellationToken ct) => db.Roles.FirstOrDefaultAsync(x => x.Id == roleId && x.DeletedAt == null, ct);
    public Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken ct) => db.UserRoles.AsNoTracking().AnyAsync(x => x.UserId == userId && x.RoleId == roleId, ct);
    public Task<Employee?> FindEmployeeAsync(Guid employeeId, CancellationToken ct) => db.Employees.FirstOrDefaultAsync(x => x.Id == employeeId && x.DeletedAt == null, ct);

    public Task<WorkflowRequestType?> FindRequestTypeAsync(Guid requestTypeId, CancellationToken ct) => db.WorkflowRequestTypes.FirstOrDefaultAsync(x => x.Id == requestTypeId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<WorkflowRequestTypeSummary>> ListRequestTypesAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, bool? active, CancellationToken ct)
    {
        var q = db.WorkflowRequestTypes.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value);
        if (active is not null) q = q.Where(x => x.IsActive == active.Value);
        return await q.OrderBy(x => x.Code).Select(x => new WorkflowRequestTypeSummary(x.Id, x.CompanyId, x.Code, x.Name, x.Description, x.SlaMinutes, x.RequiredFieldsJson, x.IsActive, x.Version)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowApprovalStepDefinition>> ListStepDefinitionsAsync(Guid requestTypeId, CancellationToken ct) =>
        await db.WorkflowApprovalStepDefinitions.Where(x => x.RequestTypeId == requestTypeId && x.DeletedAt == null).OrderBy(x => x.StepOrder).ToListAsync(ct);

    public async Task<IReadOnlyList<WorkflowApprovalStepSummary>> ListStepSummariesAsync(Guid requestTypeId, CancellationToken ct)
    {
        var steps = await db.WorkflowApprovalStepDefinitions.AsNoTracking().Where(x => x.RequestTypeId == requestTypeId && x.DeletedAt == null).OrderBy(x => x.StepOrder).ToListAsync(ct);
        var userIds = steps.Where(x => x.ApproverUserId != null).Select(x => x.ApproverUserId!.Value).Distinct().ToArray();
        var roleIds = steps.Where(x => x.ApproverRoleId != null).Select(x => x.ApproverRoleId!.Value).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Username, ct);
        var roles = await db.Roles.AsNoTracking().Where(x => roleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        return steps.Select(x => new WorkflowApprovalStepSummary(x.Id, x.RequestTypeId, x.StepOrder, x.Name, x.TargetKind, x.ApproverUserId, x.ApproverUserId is { } u && users.TryGetValue(u, out var un) ? un : null, x.ApproverRoleId, x.ApproverRoleId is { } r && roles.TryGetValue(r, out var rc) ? rc : null)).ToArray();
    }

    public void AddRequestType(WorkflowRequestType requestType) => db.WorkflowRequestTypes.Add(requestType);
    public void AddStepDefinition(WorkflowApprovalStepDefinition step) => db.WorkflowApprovalStepDefinitions.Add(step);
    public void RemoveStepDefinitions(IEnumerable<WorkflowApprovalStepDefinition> steps) => db.WorkflowApprovalStepDefinitions.RemoveRange(steps);

    public async Task<string> NextRequestNoAsync(Guid companyId, int year, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workflow.request_number_counters(company_id, year, next_value)
            VALUES (@company_id, @year, 2)
            ON CONFLICT (company_id, year)
            DO UPDATE SET next_value = workflow.request_number_counters.next_value + 1
            RETURNING next_value - 1;
            """;
        command.Parameters.Add(new NpgsqlParameter("company_id", companyId));
        command.Parameters.Add(new NpgsqlParameter("year", year));
        var value = await command.ExecuteScalarAsync(ct);
        var sequence = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        return $"REQ-{year}-{sequence:D6}";
    }

    public Task<WorkflowRequest?> FindRequestAsync(Guid requestId, CancellationToken ct) => db.WorkflowRequests.FirstOrDefaultAsync(x => x.Id == requestId && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<WorkflowRequestSummary>> ListRequestsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? employeeId, Guid? requesterUserId, string? status, int take, CancellationToken ct)
    {
        var q = db.WorkflowRequests.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId));
        if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value);
        if (employeeId is not null) q = q.Where(x => x.EmployeeId == employeeId.Value);
        if (requesterUserId is not null) q = q.Where(x => x.RequesterUserId == requesterUserId.Value);
        if (status is not null) q = q.Where(x => x.Status == status);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
        var typeIds = rows.Select(x => x.RequestTypeId).Distinct().ToArray();
        var userIds = rows.Select(x => x.RequesterUserId).Distinct().ToArray();
        var employeeIds = rows.Where(x => x.EmployeeId != null).Select(x => x.EmployeeId!.Value).Distinct().ToArray();
        var types = await db.WorkflowRequestTypes.AsNoTracking().Where(x => typeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Username, ct);
        var employees = await db.Employees.AsNoTracking().Where(x => employeeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return rows.Select(x =>
        {
            types.TryGetValue(x.RequestTypeId, out var type); users.TryGetValue(x.RequesterUserId, out var username);
            Employee? employee = null; if (x.EmployeeId is { } eid) employees.TryGetValue(eid, out employee);
            return new WorkflowRequestSummary(x.Id, x.CompanyId, x.RequestNo, x.RequestTypeId, type?.Code ?? "—", type?.Name ?? "—", x.RequesterUserId, username ?? "—", x.EmployeeId, employee?.EmployeeNo, employee is null ? null : $"{employee.FirstName} {employee.LastName}", x.Priority, x.RequestDataJson, x.Status, x.CurrentStepOrder, x.SlaMinutesSnapshot, x.SubmittedAt, x.DueAt, x.ResolvedAt, x.Version);
        }).ToArray();
    }

    public Task<WorkflowRequestApproval?> FindCurrentApprovalAsync(Guid requestId, int stepOrder, CancellationToken ct) => db.WorkflowRequestApprovals.FirstOrDefaultAsync(x => x.RequestId == requestId && x.StepOrder == stepOrder && x.Status == WorkflowApprovalStatuses.Pending, ct);
    public Task<WorkflowRequestApproval?> FindApprovalAsync(Guid requestId, int stepOrder, CancellationToken ct) => db.WorkflowRequestApprovals.FirstOrDefaultAsync(x => x.RequestId == requestId && x.StepOrder == stepOrder, ct);

    public async Task<IReadOnlyList<WorkflowRequestApprovalSummary>> ListApprovalsAsync(Guid requestId, CancellationToken ct)
    {
        var rows = await db.WorkflowRequestApprovals.AsNoTracking().Where(x => x.RequestId == requestId).OrderBy(x => x.StepOrder).ToListAsync(ct);
        var userIds = rows.Where(x => x.ApproverUserIdSnapshot != null).Select(x => x.ApproverUserIdSnapshot!.Value).Concat(rows.Where(x => x.ActionByUserId != null).Select(x => x.ActionByUserId!.Value)).Distinct().ToArray();
        var roleIds = rows.Where(x => x.ApproverRoleIdSnapshot != null).Select(x => x.ApproverRoleIdSnapshot!.Value).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Username, ct);
        var roles = await db.Roles.AsNoTracking().Where(x => roleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        return rows.Select(x => new WorkflowRequestApprovalSummary(x.Id, x.RequestId, x.StepOrder, x.StepNameSnapshot, x.TargetKindSnapshot, x.ApproverUserIdSnapshot, x.ApproverUserIdSnapshot is { } au && users.TryGetValue(au, out var aun) ? aun : null, x.ApproverRoleIdSnapshot, x.ApproverRoleIdSnapshot is { } ar && roles.TryGetValue(ar, out var arc) ? arc : null, x.Status, x.ActionByUserId, x.ActionByUserId is { } action && users.TryGetValue(action, out var actionName) ? actionName : null, x.ActionAt, x.Comment)).ToArray();
    }

    public async Task<IReadOnlyList<WorkflowTimelineSummary>> ListTimelineAsync(Guid requestId, int take, CancellationToken ct)
    {
        var rows = await db.WorkflowRequestHistories.AsNoTracking().Where(x => x.RequestId == requestId).OrderByDescending(x => x.OccurredAt).Take(take).ToListAsync(ct);
        var ids = rows.Select(x => x.ActorUserId).Distinct().ToArray(); var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Username, ct);
        return rows.Select(x => new WorkflowTimelineSummary(x.Id, x.RequestId, x.EventType, x.FromStatus, x.ToStatus, x.ActorUserId, users.TryGetValue(x.ActorUserId, out var name) ? name : "—", x.OccurredAt, x.DetailsJson)).ToArray();
    }

    public void AddRequest(WorkflowRequest request) => db.WorkflowRequests.Add(request);
    public void AddApproval(WorkflowRequestApproval approval) => db.WorkflowRequestApprovals.Add(approval);
    public void AddHistory(WorkflowRequestHistory history) => db.WorkflowRequestHistories.Add(history);

    public async Task<IReadOnlyList<WorkflowSlaCandidate>> BuildSlaCandidatesAsync(DateTimeOffset now, IReadOnlyCollection<Guid>? companyIds, CancellationToken ct)
    {
        var q = db.WorkflowRequests.AsNoTracking().Where(x => x.DeletedAt == null && x.Status == WorkflowRequestStatuses.InApproval && x.SubmittedAt != null && x.DueAt != null);
        if (companyIds is not null) q = q.Where(x => companyIds.Contains(x.CompanyId));
        var rows = await q.ToListAsync(ct); var result = new List<WorkflowSlaCandidate>();
        foreach (var row in rows)
        {
            var submitted = row.SubmittedAt!.Value; var due = row.DueAt!.Value; var warningAt = submitted.AddMinutes(Math.Max(1, row.SlaMinutesSnapshot * 0.75)); var escalationAt = due.AddMinutes(Math.Max(60, row.SlaMinutesSnapshot / 4));
            if (now >= warningAt) result.Add(Candidate(row, "WORKFLOW_SLA_WARNING", WorkflowPriorities.Important, $"Talep SLA süresinin %75'ine ulaştı: {row.RequestNo}.", new { row.RequestNo, row.CurrentStepOrder, dueAt = due }));
            if (now >= due) result.Add(Candidate(row, "WORKFLOW_SLA_OVERDUE", WorkflowPriorities.Critical, $"Talep SLA süresini aştı: {row.RequestNo}.", new { row.RequestNo, row.CurrentStepOrder, dueAt = due }));
            if (now >= escalationAt) result.Add(Candidate(row, "WORKFLOW_SLA_ESCALATION", WorkflowPriorities.Critical, $"Talep SLA escalation eşiğini aştı: {row.RequestNo}.", new { row.RequestNo, row.CurrentStepOrder, dueAt = due, escalationAt }));
        }
        return result;
    }

    public async Task<bool> TryInsertSlaEventAsync(WorkflowSlaCandidate c, DateTimeOffset createdAt, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO workflow.sla_events(id, company_id, request_id, event_type, severity, dedupe_key, message, metadata_json, created_at)
            VALUES ({id}, {c.CompanyId}, {c.RequestId}, {c.EventType}, {c.Severity}, {c.DedupeKey}, {c.Message}, CAST({c.MetadataJson} AS jsonb), {createdAt.ToUniversalTime()})
            ON CONFLICT (dedupe_key) DO NOTHING
            """, ct);
        return affected > 0;
    }

    public async Task<IReadOnlyList<WorkflowSlaEventSummary>> ListSlaEventsAsync(bool globalAccess, IReadOnlyCollection<Guid> companyIds, Guid? companyId, Guid? requestId, string? eventType, int take, CancellationToken ct)
    {
        var q = db.WorkflowSlaEvents.AsNoTracking().AsQueryable(); if (!globalAccess) q = q.Where(x => companyIds.Contains(x.CompanyId)); if (companyId is not null) q = q.Where(x => x.CompanyId == companyId.Value); if (requestId is not null) q = q.Where(x => x.RequestId == requestId.Value); if (eventType is not null) q = q.Where(x => x.EventType == eventType);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct); var requestIds = rows.Select(x => x.RequestId).Distinct().ToArray(); var requests = await db.WorkflowRequests.AsNoTracking().Where(x => requestIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.RequestNo, ct);
        return rows.Select(x => new WorkflowSlaEventSummary(x.Id, x.CompanyId, x.RequestId, requests.TryGetValue(x.RequestId, out var no) ? no : "—", x.EventType, x.Severity, x.Message, x.MetadataJson, x.CreatedAt)).ToArray();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static WorkflowSlaCandidate Candidate(WorkflowRequest row, string eventType, string severity, string message, object metadata) => new(row.CompanyId, row.Id, eventType, severity, $"{eventType}:{row.Id:N}", message, JsonSerializer.Serialize(metadata));
}
