using PersonnelPlatform.Domain.Integration;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class IntegrationEntityTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SystemId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void System_Create_NormalizesCodeAndType()
    {
        var row = IntegrationSystem.Create(CompanyId, " pdks_main ", "Ana PDKS", "pdks", Now, ActorId);
        Assert.Equal("PDKS_MAIN", row.Code);
        Assert.Equal(IntegrationSystemTypes.Pdks, row.SystemType);
        Assert.True(row.IsActive);
    }

    [Fact]
    public void MealTerminal_StoresCampScopeAndCredentialHash()
    {
        var campId = Guid.NewGuid();
        var row = IntegrationDevice.Create(CompanyId, SystemId, " meal_01 ", "Yemek 1", IntegrationDeviceTypes.MealTerminal, campId, new string('A', 64), Now, ActorId);
        Assert.Equal("MEAL_01", row.Code);
        Assert.Equal(campId, row.ScopedCampId);
        Assert.Equal(new string('A', 64), row.CredentialHash);
    }

    [Fact]
    public void Mapping_NormalizesExternalCodeAndEntityType()
    {
        var target = Guid.NewGuid();
        var row = ExternalEntityMapping.Create(CompanyId, SystemId, "employee", " emp-001 ", target, Now, ActorId);
        Assert.Equal(IntegrationEntityTypes.Employee, row.EntityType);
        Assert.Equal("EMP-001", row.ExternalCode);
        Assert.Equal(target, row.InternalEntityId);
    }

    [Fact]
    public void Staging_TechnicalFailure_RetriesThenDeadLetters()
    {
        var row = IntegrationStagingRecord.Create(CompanyId, SystemId, Guid.NewGuid(), IntegrationEventTypes.AttendanceEvent, "evt-1", "{}", Now);
        Assert.Equal(IntegrationStagingStatuses.Received, row.Status);

        row.BeginProcessing(Now.AddSeconds(1));
        row.TechnicalError("NETWORK", "Temporary error", 2, Now.AddSeconds(2));
        Assert.Equal(IntegrationStagingStatuses.TechnicalError, row.Status);
        Assert.NotNull(row.NextRetryAt);
        Assert.Equal(1, row.AttemptCount);

        row.BeginProcessing(Now.AddMinutes(3));
        row.TechnicalError("NETWORK", "Still failing", 2, Now.AddMinutes(3).AddSeconds(1));
        Assert.Equal(IntegrationStagingStatuses.DeadLetter, row.Status);
        Assert.Null(row.NextRetryAt);
        Assert.Equal(2, row.AttemptCount);
    }

    [Fact]
    public void BusinessError_CanBeRequeuedWithVersionReset()
    {
        var row = IntegrationStagingRecord.Create(CompanyId, SystemId, null, IntegrationEventTypes.MealConsumption, "meal-1", "{}", Now);
        row.BeginProcessing(Now.AddSeconds(1));
        row.BusinessError("MAPPING_NOT_FOUND", "Mapping missing", Now.AddSeconds(2));
        var versionBefore = row.Version;

        var previous = row.Requeue(Now.AddMinutes(1), ActorId);

        Assert.Equal(IntegrationStagingStatuses.BusinessError, previous);
        Assert.Equal(IntegrationStagingStatuses.Received, row.Status);
        Assert.Equal(0, row.AttemptCount);
        Assert.Null(row.ErrorCode);
        Assert.True(row.Version > versionBefore);
    }

    [Fact]
    public void ProcessedStaging_IsTerminalForRequeue()
    {
        var row = IntegrationStagingRecord.Create(CompanyId, SystemId, null, IntegrationEventTypes.AttendanceEvent, "evt-2", "{}", Now);
        row.BeginProcessing(Now.AddSeconds(1));
        row.Complete("RAW_ATTENDANCE_EVENT", Guid.NewGuid(), Now.AddSeconds(2));
        Assert.Throws<InvalidOperationException>(() => row.Requeue(Now.AddMinutes(1), ActorId));
    }
}
