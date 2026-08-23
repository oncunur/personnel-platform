using PersonnelPlatform.Domain.Documents;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class DocumentHistoryTests
{
    [Fact]
    public void History_should_normalize_action_and_preserve_transition()
    {
        var documentId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 8, 23, 5, 30, 0, TimeSpan.Zero);

        var history = EmployeeDocumentHistory.Create(documentId, " renewed ", "EXPIRING", "VALID", actor, at, "Yeni belge yüklendi.");

        Assert.Equal("RENEWED", history.Action);
        Assert.Equal("EXPIRING", history.FromStatus);
        Assert.Equal("VALID", history.ToStatus);
        Assert.Equal(actor, history.ChangedBy);
        Assert.Equal(at, history.ChangedAt);
    }
}
