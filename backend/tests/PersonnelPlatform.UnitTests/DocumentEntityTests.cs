using PersonnelPlatform.Domain.Documents;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class DocumentEntityTests
{
    [Fact]
    public void Document_type_should_normalize_code_and_reminders()
    {
        var actor = Guid.NewGuid();
        var type = DocumentType.Create(" medical_report ", "Sağlık Raporu", null, false, true, null, true, false, true, [30, 90, 30, 7], 10, DateTimeOffset.UtcNow, actor);

        Assert.Equal("MEDICAL_REPORT", type.Code);
        Assert.Equal([90, 30, 7], type.ReminderDays());
    }

    [Fact]
    public void Renewed_document_history_can_archive_previous_record()
    {
        var actor = Guid.NewGuid();
        var document = EmployeeDocument.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DOC-1", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), null, "TR", null, null, DateTimeOffset.UtcNow, actor);

        document.Archive(DateTimeOffset.UtcNow.AddMinutes(1), actor);

        Assert.Equal(EmployeeDocumentStatuses.Archived, document.Status);
        Assert.Equal(2, document.Version);
    }

    [Fact]
    public void Stored_file_should_start_pending_and_become_active()
    {
        var file = StoredFile.CreatePending("file.pdf", "documents/2026/08/abc.pdf", "application/pdf", ".pdf", 100, new string('a', 64), "LOCAL", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(StoredFileStatuses.Pending, file.Status);
        file.Activate();
        Assert.Equal(StoredFileStatuses.Active, file.Status);
    }
}
