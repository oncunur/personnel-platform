using PersonnelPlatform.Application.Reporting;
using PersonnelPlatform.Domain.Finance;
using PersonnelPlatform.Domain.Reporting;
using Xunit;

namespace PersonnelPlatform.UnitTests;

public sealed class FinanceReportingTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid PeriodId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CostEntry_Create_NormalizesAndRoundsSnapshot()
    {
        var row = CostEntry.Create(CompanyId, " payroll ", Guid.NewGuid(), "line-1", EmployeeId, ProjectId, null,
            new DateOnly(2026, 8, 31), "payroll", 33.333333m, "percent", 123.456m, "try", "attendance", "{}", Now);

        Assert.Equal(CostSourceTypes.Payroll, row.SourceType);
        Assert.Equal(CostCategories.Payroll, row.Category);
        Assert.Equal(CostAllocationBases.Attendance, row.AllocationBasis);
        Assert.Equal(33.3333m, row.Quantity);
        Assert.Equal(123.46m, row.Amount);
        Assert.Equal("TRY", row.Currency);
    }

    [Fact]
    public void PayrollAllocation_RejectsPercentAboveHundred()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PayrollCostAllocationOverride.Create(PeriodId, CompanyId, EmployeeId, ProjectId, null, 100.01m, Now, UserId));
    }

    [Fact]
    public void ReportExport_TransitionsQueuedProcessingCompleted()
    {
        var row = ReportExportJob.Create(CompanyId, UserId, "project_360", "xlsx", "{}", Now);
        Assert.Equal(ReportExportStatuses.Queued, row.Status);
        row.Start(Now.AddMinutes(1));
        Assert.Equal(ReportExportStatuses.Processing, row.Status);
        row.Complete("reports/a.xlsx", "a.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 128, Now.AddMinutes(2));
        Assert.Equal(ReportExportStatuses.Completed, row.Status);
        Assert.Equal(3, row.Version);
        Assert.Throws<InvalidOperationException>(() => row.Start(Now.AddMinutes(3)));
    }

    [Fact]
    public void ReportBinaryWriter_ProducesXlsxZipAndPdfHeader()
    {
        var table = new ReportTable("Test", ["A", "B"], [new[] { "1", "2" }]);
        var xlsx = ReportBinaryWriter.WriteXlsx(table);
        var pdf = ReportBinaryWriter.WritePdf(table);

        Assert.True(xlsx.Length > 4);
        Assert.Equal((byte)'P', xlsx[0]);
        Assert.Equal((byte)'K', xlsx[1]);
        Assert.True(pdf.Length > 8);
        Assert.Equal("%PDF-1.4", System.Text.Encoding.ASCII.GetString(pdf, 0, 8));
    }
}
