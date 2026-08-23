using PersonnelPlatform.Domain.Attendance;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class OvertimeRequestTests
{
    [Fact]
    public void Request_snapshots_candidate_and_daily_version()
    {
        var row = Create(180, 120, sourceDailyVersion: 4);

        Assert.Equal(180, row.CandidateMinutes);
        Assert.Equal(120, row.RequestedMinutes);
        Assert.Equal(4, row.SourceDailyVersion);
        Assert.Equal(OvertimeRequestStatuses.PendingManager, row.Status);
        Assert.Equal(0, row.ApprovedMinutes);
    }

    [Fact]
    public void Requested_minutes_cannot_exceed_candidate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(60, 90));
    }

    [Fact]
    public void Manager_then_hr_approval_sets_only_final_approved_minutes()
    {
        var row = Create(180, 150);
        var manager = Guid.NewGuid();
        var hr = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        row.ApproveManager(manager, "uygun", now);
        Assert.Equal(OvertimeRequestStatuses.PendingHr, row.Status);
        Assert.Equal(0, row.ApprovedMinutes);

        row.ApproveHr(hr, 120, "120 dk onay", now.AddMinutes(1));
        Assert.Equal(OvertimeRequestStatuses.Approved, row.Status);
        Assert.Equal(120, row.ApprovedMinutes);
        Assert.Equal(hr, row.HrDecidedBy);
    }

    [Fact]
    public void Hr_cannot_approve_more_than_requested()
    {
        var row = Create(180, 120);
        row.ApproveManager(Guid.NewGuid(), null, DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(() => row.ApproveHr(Guid.NewGuid(), 121, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Rejection_keeps_approved_minutes_zero()
    {
        var row = Create(180, 120);
        row.Reject(Guid.NewGuid(), "uygun değil", DateTimeOffset.UtcNow);

        Assert.Equal(OvertimeRequestStatuses.Rejected, row.Status);
        Assert.Equal(0, row.ApprovedMinutes);
    }

    [Fact]
    public void Request_cannot_be_cancelled_after_manager_approval()
    {
        var row = Create(180, 120);
        row.ApproveManager(Guid.NewGuid(), null, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => row.Cancel(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    private static OvertimeRequest Create(int candidateMinutes, int requestedMinutes, int sourceDailyVersion = 1) =>
        OvertimeRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            sourceDailyVersion,
            new DateOnly(2026, 8, 24),
            candidateMinutes,
            requestedMinutes,
            "proje teslimi",
            DateTimeOffset.UtcNow,
            Guid.NewGuid());
}
