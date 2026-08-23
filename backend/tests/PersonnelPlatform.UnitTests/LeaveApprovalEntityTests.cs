using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Personnel;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class LeaveApprovalEntityTests
{
    [Fact]
    public void Manager_approval_can_move_from_pending_to_approved()
    {
        var leaveId = Guid.NewGuid();
        var managerEmployeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var approval = LeaveApproval.Create(leaveId, 1, LeaveApprovalStepCodes.Manager, managerEmployeeId, userId, true, now, userId);

        approval.Approve(userId, "Uygun", now.AddMinutes(1));

        Assert.Equal(LeaveApprovalStatuses.Approved, approval.Status);
        Assert.Equal(userId, approval.DecidedByUserId);
        Assert.Equal("Uygun", approval.DecisionNote);
        Assert.Equal(2, approval.Version);
    }

    [Fact]
    public void Waiting_HR_step_must_be_activated_before_decision()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var approval = LeaveApproval.Create(Guid.NewGuid(), 2, LeaveApprovalStepCodes.Hr, null, null, false, now, userId);

        Assert.Throws<InvalidOperationException>(() => approval.Approve(userId, null, now));

        approval.Activate(now.AddMinutes(1), userId);
        approval.Approve(userId, null, now.AddMinutes(2));
        Assert.Equal(LeaveApprovalStatuses.Approved, approval.Status);
    }

    [Fact]
    public void User_employee_link_can_be_relinked_with_version_increment()
    {
        var userId = Guid.NewGuid();
        var firstEmployee = Guid.NewGuid();
        var secondEmployee = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var link = EmployeeUserLink.Create(userId, firstEmployee, now, actor);

        link.Relink(secondEmployee, now.AddMinutes(1), actor);

        Assert.Equal(secondEmployee, link.EmployeeId);
        Assert.True(link.IsActive);
        Assert.Equal(2, link.Version);
    }

    [Fact]
    public void Manager_step_requires_manager_employee_identity()
    {
        Assert.Throws<ArgumentException>(() => LeaveApproval.Create(
            Guid.NewGuid(),
            1,
            LeaveApprovalStepCodes.Manager,
            null,
            null,
            true,
            DateTimeOffset.UtcNow,
            Guid.NewGuid()));
    }
}
