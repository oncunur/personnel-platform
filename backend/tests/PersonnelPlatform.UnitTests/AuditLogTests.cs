using PersonnelPlatform.Domain.Audit;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class AuditLogTests
{
    [Fact]
    public void Audit_log_should_capture_security_event_without_mutation_api()
    {
        var occurredAt = new DateTimeOffset(2026, 8, 23, 4, 30, 0, TimeSpan.Zero);
        var actorUserId = Guid.NewGuid();

        var log = AuditLog.Create(
            "SECURITY",
            "AUTH_LOGOUT_ALL",
            true,
            "INFO",
            occurredAt,
            actorUserId,
            "admin",
            "127.0.0.1",
            "test-agent",
            "trace-1",
            "USER",
            actorUserId.ToString());

        Assert.Equal("SECURITY", log.Category);
        Assert.Equal("AUTH_LOGOUT_ALL", log.EventType);
        Assert.True(log.Succeeded);
        Assert.Equal(actorUserId, log.ActorUserId);
        Assert.Equal(occurredAt, log.OccurredAt);
    }
}
