namespace PersonnelPlatform.Application.Workflow;

public static class WorkflowPermissions
{
    public const string RequestTypeView = "workflow.request_type.view";
    public const string RequestTypeManage = "workflow.request_type.manage";
    public const string RequestView = "workflow.request.view";
    public const string RequestCreate = "workflow.request.create";
    public const string RequestManage = "workflow.request.manage";
    public const string RequestApprove = "workflow.request.approve";
    public const string SlaView = "workflow.sla.view";
    public const string SlaProcess = "workflow.sla.process";
}

public sealed record CreateWorkflowRequestTypeRequest(Guid CompanyId, string Code, string Name, string? Description, int SlaMinutes, string RequiredFieldsJson);
public sealed record UpdateWorkflowRequestTypeRequest(int Version, string Name, string? Description, int SlaMinutes, string RequiredFieldsJson, bool IsActive);
public sealed record WorkflowRequestTypeSummary(Guid Id, Guid CompanyId, string Code, string Name, string? Description, int SlaMinutes, string RequiredFieldsJson, bool IsActive, int Version);

public sealed record WorkflowApprovalStepRequest(int StepOrder, string Name, string TargetKind, Guid? ApproverUserId, Guid? ApproverRoleId);
public sealed record ReplaceWorkflowApprovalStepsRequest(int RequestTypeVersion, IReadOnlyList<WorkflowApprovalStepRequest> Steps);
public sealed record WorkflowApprovalStepSummary(Guid Id, Guid RequestTypeId, int StepOrder, string Name, string TargetKind, Guid? ApproverUserId, string? ApproverUsername, Guid? ApproverRoleId, string? ApproverRoleCode);

public sealed record CreateWorkflowRequestRequest(Guid CompanyId, Guid RequestTypeId, Guid? EmployeeId, string Priority, string RequestDataJson);
public sealed record WorkflowRequestActionRequest(int Version, string? Comment = null);
public sealed record WorkflowRequestSummary(Guid Id, Guid CompanyId, string RequestNo, Guid RequestTypeId, string RequestTypeCode, string RequestTypeName, Guid RequesterUserId, string RequesterUsername, Guid? EmployeeId, string? EmployeeNo, string? EmployeeName, string Priority, string RequestDataJson, string Status, int CurrentStepOrder, int SlaMinutesSnapshot, DateTimeOffset? SubmittedAt, DateTimeOffset? DueAt, DateTimeOffset? ResolvedAt, int Version);
public sealed record WorkflowRequestApprovalSummary(Guid Id, Guid RequestId, int StepOrder, string StepName, string TargetKind, Guid? ApproverUserId, string? ApproverUsername, Guid? ApproverRoleId, string? ApproverRoleCode, string Status, Guid? ActionByUserId, string? ActionByUsername, DateTimeOffset? ActionAt, string? Comment);
public sealed record WorkflowTimelineSummary(Guid Id, Guid RequestId, string EventType, string? FromStatus, string ToStatus, Guid ActorUserId, string ActorUsername, DateTimeOffset OccurredAt, string DetailsJson);
public sealed record WorkflowRequestDetail(WorkflowRequestSummary Request, IReadOnlyList<WorkflowRequestApprovalSummary> Approvals, IReadOnlyList<WorkflowTimelineSummary> Timeline);

public sealed record WorkflowSlaEventSummary(Guid Id, Guid CompanyId, Guid RequestId, string RequestNo, string EventType, string Severity, string Message, string MetadataJson, DateTimeOffset CreatedAt);
public sealed record WorkflowSlaRunResult(int Candidates, int Created, int Duplicates);

public sealed record WorkflowResult<T>(bool Succeeded, T? Value, string? ErrorCode, string? ErrorMessage) where T : class
{
    public static WorkflowResult<T> Success(T value) => new(true, value, null, null);
    public static WorkflowResult<T> Failure(string code, string message) => new(false, null, code, message);
}
