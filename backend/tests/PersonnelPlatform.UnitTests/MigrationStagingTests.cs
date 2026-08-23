using PersonnelPlatform.Domain.Migration;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class MigrationStagingTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 20, 0, 0, TimeSpan.Zero);
    private static readonly string HashA = new('A', 64);
    private static readonly string HashB = new('B', 64);

    [Fact]
    public void Run_WithCleanRowsAndMatchingReconciliation_ReachesReconciled()
    {
        var run = CreateRun();
        run.CompleteStaging(3, 2, 1, 0, 0, Now.AddMinutes(1), ActorId);
        Assert.Equal(MigrationRunStatuses.Staged, run.Status);

        run.CompleteValidation(Now.AddMinutes(2), ActorId);
        Assert.Equal(MigrationRunStatuses.Validated, run.Status);

        run.CompleteReconciliation(0, Now.AddMinutes(3), ActorId);
        Assert.Equal(MigrationRunStatuses.Reconciled, run.Status);
        Assert.Equal(0, run.ReconciliationMismatchCount);
    }

    [Fact]
    public void Run_WithRowErrors_RemainsBlockedAfterReconciliation()
    {
        var run = CreateRun();
        run.CompleteStaging(2, 1, 0, 1, 0, Now.AddMinutes(1), ActorId);
        run.CompleteValidation(Now.AddMinutes(2), ActorId);
        Assert.Equal(MigrationRunStatuses.Blocked, run.Status);

        run.CompleteReconciliation(0, Now.AddMinutes(3), ActorId);
        Assert.Equal(MigrationRunStatuses.Blocked, run.Status);
    }

    [Fact]
    public void StageRow_UnchangedSource_IsDuplicateWithoutExposingPayloadSemantics()
    {
        var row = MigrationStageRow.Create(Guid.NewGuid(), 1, "EMP-001", "HR|EMP|EMP-001|Employee", HashA, "cipher-a", "cipher-b",
            MigrationIdempotenceStatuses.Unchanged, Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, Now);
        Assert.Equal(MigrationStageRowStatuses.Duplicate, row.Status);
        Assert.Equal(MigrationIdempotenceStatuses.Unchanged, row.IdempotenceStatus);
        Assert.Equal("cipher-a", row.SourcePayloadCiphertext);
    }

    [Fact]
    public void StageRow_ErrorOverridesDuplicateClassification()
    {
        var row = MigrationStageRow.Create(Guid.NewGuid(), 1, "EMP-001", "HR|EMP|EMP-001|Employee", HashA, "cipher-a", "cipher-b",
            MigrationIdempotenceStatuses.Unchanged, Guid.NewGuid(), Guid.NewGuid(), null, null, "DATE_INVALID", "Invalid hire date", Now);
        Assert.Equal(MigrationStageRowStatuses.Error, row.Status);
        Assert.Equal("DATE_INVALID", row.ErrorCode);
    }

    [Fact]
    public void Lineage_ClassifiesSameHashAsUnchangedAndNewHashAsChanged()
    {
        var runId = Guid.NewGuid();
        var rowId = Guid.NewGuid();
        var lineage = MigrationLineageRecord.Create(CompanyId, "HR", "EMPLOYEE", "EMP-001", "Employee", HashA, runId, rowId, Now, ActorId);

        Assert.Equal(MigrationIdempotenceStatuses.Unchanged, lineage.Classify(HashA));
        Assert.Equal(MigrationIdempotenceStatuses.Changed, lineage.Classify(HashB));

        var nextRun = Guid.NewGuid();
        var nextRow = Guid.NewGuid();
        lineage.Observe(HashB, nextRun, nextRow, Now.AddMinutes(1), ActorId);
        Assert.Equal(HashB, lineage.LastSourceRowHash);
        Assert.Equal(2, lineage.SeenCount);
        Assert.Equal(nextRun, lineage.LastRunId);
    }

    [Fact]
    public void Reconciliation_UsesAbsoluteDifferenceAndTolerance()
    {
        var match = MigrationReconciliation.Create(Guid.NewGuid(), "EMP_COUNT", "Employee count", 100m, 100m, 0m, null, Now, ActorId);
        var mismatch = MigrationReconciliation.Create(Guid.NewGuid(), "PAYROLL_TOTAL", "Payroll total", 1000m, 1001.01m, 1m, null, Now, ActorId);
        Assert.Equal(MigrationReconciliationStatuses.Match, match.Status);
        Assert.Equal(MigrationReconciliationStatuses.Mismatch, mismatch.Status);
        Assert.Equal(1.01m, mismatch.Difference);
    }

    [Fact]
    public void Run_RejectsInvalidSha256Hash()
    {
        Assert.Throws<ArgumentException>(() => MigrationRun.Create(CompanyId, "HR", "EMPLOYEE", "Employee", "employees.csv", "not-a-hash", HashA, Now, ActorId));
    }

    private static MigrationRun CreateRun() => MigrationRun.Create(CompanyId, "HR", "EMPLOYEE", "Employee", "employees.csv", HashA, HashB, Now, ActorId);
}