using System.IO.Compression;
using System.Text;
using PersonnelPlatform.Application.Integration;
using PersonnelPlatform.Domain.Integration;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class ImportErpTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SystemId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ImportJob_FinishesPartial_WhenSomeRowsFail()
    {
        var job = ImportJob.Create(CompanyId, SystemId, ImportTargetTypes.ErpAccountMapping, "mapping.xlsx", new string('A', 64), "[\"Cost Category\",\"Account Code\"]", 3, Now, ActorId);
        job.ApplyMapping("{\"COST_CATEGORY\":\"Cost Category\",\"ACCOUNT_CODE\":\"Account Code\"}", Now.AddMinutes(1), ActorId);
        job.Begin(Now.AddMinutes(2), ActorId);
        job.Finish(2, 1, Now.AddMinutes(3), ActorId);

        Assert.Equal(ImportJobStatuses.Partial, job.Status);
        Assert.Equal(2, job.SuccessRows);
        Assert.Equal(1, job.ErrorRows);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void ImportRow_CannotBeProcessedTwice()
    {
        var row = ImportRow.Create(Guid.NewGuid(), 2, "{\"A\":\"1\"}");
        row.MarkImported("ERP_ACCOUNT_MAPPING", Guid.NewGuid(), Now);
        Assert.Throws<InvalidOperationException>(() => row.MarkError("ERR", "error", Now.AddMinutes(1)));
    }

    [Fact]
    public void ErpBatch_CannotCloseBeforeAccepted()
    {
        var batch = ErpExportBatch.Create(CompanyId, SystemId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), Now, ActorId);
        Assert.Throws<InvalidOperationException>(() => batch.Close(Now.AddMinutes(1), ActorId));
        batch.MarkSent(Now.AddMinutes(1), ActorId);
        batch.MarkReconciled(ErpBatchStatuses.Accepted, Now.AddMinutes(2), ActorId);
        batch.Close(Now.AddMinutes(3), ActorId);
        Assert.Equal(ErpBatchStatuses.Closed, batch.Status);
    }

    [Fact]
    public void ErpLine_ReconciliationCalculatesVariance()
    {
        var line = ErpExportLine.Create(Guid.NewGuid(), Guid.NewGuid(), "LINE1", "PAYROLL", Guid.NewGuid(), null, null, null, new DateOnly(2026, 8, 23), "PAYROLL", "740.01", null, 100m, "TRY");
        line.MarkSent();
        line.Reconcile(ErpLineStatuses.Accepted, 97.50m, "ERP-1", null, null, Now);
        Assert.Equal(ErpLineStatuses.Accepted, line.Status);
        Assert.Equal(-2.50m, line.VarianceAmount);
    }

    [Fact]
    public void SpreadsheetReader_ReadsFirstWorksheetAndHeaders()
    {
        var bytes = TinyWorkbook();
        var sheet = SpreadsheetImportReader.ReadXlsx("mapping.xlsx", bytes);
        Assert.Equal(new[] { "COST_CATEGORY", "ACCOUNT_CODE" }, sheet.Headers);
        Assert.Single(sheet.Rows);
        Assert.Equal(2, sheet.Rows[0].RowNumber);
        Assert.Equal("PAYROLL", sheet.Rows[0].Values["COST_CATEGORY"]);
        Assert.Equal("740.01", sheet.Rows[0].Values["ACCOUNT_CODE"]);
    }

    private static byte[] TinyWorkbook()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using var stream = entry.Open();
            var xml = """<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>COST_CATEGORY</t></is></c><c r="B1" t="inlineStr"><is><t>ACCOUNT_CODE</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>PAYROLL</t></is></c><c r="B2" t="inlineStr"><is><t>740.01</t></is></c></row></sheetData></worksheet>""";
            var bytes = Encoding.UTF8.GetBytes(xml);
            stream.Write(bytes);
        }
        return output.ToArray();
    }
}
