using PersonnelPlatform.Domain.Workflow;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class WorkflowEntityTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Request_without_steps_is_approved_on_submit()
    {
        var row = WorkflowRequest.Create(Company, "REQ-2026-000001", Guid.NewGuid(), Actor, null, WorkflowPriorities.Normal, "{}", Now);
        row.Submit(60, 0, Now.AddMinute(), Actor);
        Assert.Equal(WorkflowRequestStatuses.Approved, row.Status);
        Assert.NotNull(row.ResolvedAt);
        Assert.Equal(2, row.Version);
    }

    [Fact]
    public void Multi_step_request_advances_then_approves()
    {
        var row = WorkflowRequest.Create(Company, "REQ-2026-000002", Guid.NewGuid(), Actor, null, WorkflowPriorities.Important, "{}", Now);
        row.Submit(120, 2, Now.AddMinute(), Actor);
        Assert.Equal(1, row.CurrentStepOrder);
        row.AdvanceApproval(1, true, Now.AddMinutes(2), Actor);
        Assert.Equal(2, row.CurrentStepOrder);
        row.AdvanceApproval(2, false, Now.AddMinutes(3), Actor);
        Assert.Equal(WorkflowRequestStatuses.Approved, row.Status);
    }

    [Fact]
    public void Reject_is_terminal()
    {
        var row = WorkflowRequest.Create(Company, "REQ-2026-000003", Guid.NewGuid(), Actor, null, WorkflowPriorities.Critical, "{}", Now);
        row.Submit(60, 1, Now.AddMinute(), Actor);
        row.Reject(1, Now.AddMinutes(2), Actor);
        Assert.Equal(WorkflowRequestStatuses.Rejected, row.Status);
        Assert.Throws<InvalidOperationException>(() => row.Cancel(Now.AddMinutes(3), Actor));
    }

    [Fact]
    public void User_step_requires_only_user_target()
    {
        Assert.Throws<ArgumentException>(() => WorkflowApprovalStepDefinition.Create(Company, Guid.NewGuid(), 1, "Manager", ApprovalTargetKinds.User, null, null, Now, Actor));
    }

    [Fact]
    public void Approval_can_only_be_decided_once()
    {
        var approval = WorkflowRequestApproval.Create(Company, Guid.NewGuid(), 1, "Manager", ApprovalTargetKinds.User, Actor, null, true);
        approval.Approve(Actor, "ok", Now);
        Assert.Equal(WorkflowApprovalStatuses.Approved, approval.Status);
        Assert.Throws<InvalidOperationException>(() => approval.Reject(Actor, "late", Now.AddMinute()));
    }
}

internal static class WorkflowTestDateExtensions
{
    public static DateTimeOffset AddMinute(this DateTimeOffset value) => value.AddMinutes(1);
}
