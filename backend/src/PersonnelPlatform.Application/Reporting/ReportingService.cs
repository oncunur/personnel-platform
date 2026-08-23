using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;
using PersonnelPlatform.Application.Authorization;
using PersonnelPlatform.Application.Finance;
using PersonnelPlatform.Application.Organization;
using PersonnelPlatform.Domain.Reporting;

namespace PersonnelPlatform.Application.Reporting;

public sealed class ReportingService(
    IReportingRepository repository,
    IOrganizationRepository organizationRepository,
    AccessControlService accessControlService,
    IReportFileStorage fileStorage,
    TimeProvider timeProvider)
{
    public async Task<ReportingResult<Project360Summary>> GetProject360Async(Guid userId, Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var project = await organizationRepository.FindProjectAsync(projectId, ct);
        if (project is null) return ReportingResult<Project360Summary>.Failure("PROJECT_NOT_FOUND", "Proje bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, project.CompanyId, ct))
            return ReportingResult<Project360Summary>.Failure("SCOPE_DENIED", "Proje şirket kapsamına erişiminiz yok.");
        var range = ResolveRange(from, to);
        if (!range.Valid) return ReportingResult<Project360Summary>.Failure("REPORT_DATE_RANGE_INVALID", "Rapor tarih aralığı geçersiz.");
        var result = await repository.GetProject360Async(projectId, range.From, range.To, ct);
        return result is null ? ReportingResult<Project360Summary>.Failure("PROJECT_NOT_FOUND", "Proje bulunamadı.") : ReportingResult<Project360Summary>.Success(result);
    }

    public async Task<ReportingResult<IReadOnlyList<ManagementProjectSummary>>> ListManagementAsync(Guid userId, Guid companyId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId, ct))
            return ReportingResult<IReadOnlyList<ManagementProjectSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var range = ResolveRange(from, to);
        if (!range.Valid) return ReportingResult<IReadOnlyList<ManagementProjectSummary>>.Failure("REPORT_DATE_RANGE_INVALID", "Rapor tarih aralığı geçersiz.");
        return ReportingResult<IReadOnlyList<ManagementProjectSummary>>.Success(await repository.ListManagementAsync(companyId, range.From, range.To, ct));
    }

    public async Task<ReportingResult<ReportExportJobSummary>> CreateExportAsync(Guid userId, CreateReportExportRequest request, CancellationToken ct)
    {
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, request.CompanyId, ct))
            return ReportingResult<ReportExportJobSummary>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        var type = Normalize(request.ReportType);
        var format = Normalize(request.Format);
        if (!ReportTypes.IsKnown(type)) return ReportingResult<ReportExportJobSummary>.Failure("REPORT_TYPE_INVALID", "Rapor türü geçersiz.");
        if (!ReportExportFormats.IsKnown(format)) return ReportingResult<ReportExportJobSummary>.Failure("REPORT_FORMAT_INVALID", "Export formatı XLSX veya PDF olmalıdır.");
        var range = ResolveRange(request.From, request.To);
        if (!range.Valid) return ReportingResult<ReportExportJobSummary>.Failure("REPORT_DATE_RANGE_INVALID", "Rapor tarih aralığı geçersiz.");
        if (type == ReportTypes.Project360 && request.ProjectId is null)
            return ReportingResult<ReportExportJobSummary>.Failure("PROJECT_REQUIRED", "Project 360 export için proje zorunludur.");
        if (request.ProjectId is { } projectId)
        {
            var project = await organizationRepository.FindProjectAsync(projectId, ct);
            if (project is null || project.CompanyId != request.CompanyId)
                return ReportingResult<ReportExportJobSummary>.Failure("PROJECT_NOT_FOUND", "Şirket kapsamındaki proje bulunamadı.");
        }

        var filters = JsonSerializer.Serialize(new ReportExportFilters(request.CompanyId, request.ProjectId, range.From, range.To));
        var job = ReportExportJob.Create(request.CompanyId, userId, type, format, filters, timeProvider.GetUtcNow());
        repository.AddExportJob(job);
        await repository.SaveChangesAsync(ct);
        return ReportingResult<ReportExportJobSummary>.Success(Map(job));
    }

    public async Task<ReportingResult<IReadOnlyList<ReportExportJobSummary>>> ListExportsAsync(Guid userId, Guid? companyId, int take, CancellationToken ct)
    {
        if (companyId is not null && !await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, companyId.Value, ct))
            return ReportingResult<IReadOnlyList<ReportExportJobSummary>>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        return ReportingResult<IReadOnlyList<ReportExportJobSummary>>.Success(await repository.ListExportJobsAsync(userId, companyId, Math.Clamp(take, 1, 200), ct));
    }

    public async Task<ReportingResult<ReportExportDownload>> DownloadExportAsync(Guid userId, Guid exportJobId, CancellationToken ct)
    {
        var job = await repository.FindExportJobAsync(exportJobId, ct);
        if (job is null || job.RequestedByUserId != userId)
            return ReportingResult<ReportExportDownload>.Failure("REPORT_EXPORT_NOT_FOUND", "Export bulunamadı.");
        if (!await accessControlService.HasScopeAsync(userId, ScopeTypes.Company, job.CompanyId, ct))
            return ReportingResult<ReportExportDownload>.Failure("SCOPE_DENIED", "Şirket kapsamına erişiminiz yok.");
        if (job.Status != ReportExportStatuses.Completed || job.StorageKey is null || job.FileName is null || job.ContentType is null)
            return ReportingResult<ReportExportDownload>.Failure("REPORT_EXPORT_NOT_READY", "Export henüz indirilmeye hazır değil.");
        var stream = await fileStorage.OpenReadAsync(job.StorageKey, ct);
        return stream is null
            ? ReportingResult<ReportExportDownload>.Failure("REPORT_EXPORT_FILE_NOT_FOUND", "Export dosyası storage üzerinde bulunamadı.")
            : ReportingResult<ReportExportDownload>.Success(new ReportExportDownload(stream, job.ContentType, job.FileName));
    }

    private (bool Valid, DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var resolvedFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        var resolvedTo = to ?? today;
        return (resolvedTo >= resolvedFrom && resolvedTo.DayNumber - resolvedFrom.DayNumber <= 730, resolvedFrom, resolvedTo);
    }

    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static ReportExportJobSummary Map(ReportExportJob x) => new(x.Id, x.CompanyId, x.RequestedByUserId, x.ReportType, x.Format, x.FiltersJson, x.Status, x.FileName, x.ContentType, x.FileSizeBytes, x.StartedAt, x.CompletedAt, x.ErrorMessage, x.CreatedAt, x.Version);
}

public sealed record ReportExportFilters(Guid CompanyId, Guid? ProjectId, DateOnly From, DateOnly To);

public sealed class ReportExportProcessor(
    IReportingRepository reportingRepository,
    IFinanceRepository financeRepository,
    IReportFileStorage fileStorage,
    TimeProvider timeProvider)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var jobs = await reportingRepository.ListQueuedExportJobsAsync(5, ct);
        var completed = 0;
        foreach (var job in jobs)
        {
            try
            {
                job.Start(timeProvider.GetUtcNow());
                await reportingRepository.SaveChangesAsync(ct);
                var filters = JsonSerializer.Deserialize<ReportExportFilters>(job.FiltersJson) ?? throw new InvalidOperationException("Export filters are invalid.");
                var table = await BuildTableAsync(job.ReportType, filters, ct);
                var bytes = job.Format == ReportExportFormats.Xlsx ? ReportBinaryWriter.WriteXlsx(table) : ReportBinaryWriter.WritePdf(table);
                var extension = job.Format == ReportExportFormats.Xlsx ? "xlsx" : "pdf";
                var contentType = job.Format == ReportExportFormats.Xlsx ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/pdf";
                var fileName = $"{job.ReportType.ToLowerInvariant()}-{job.Id:N}.{extension}";
                var storageKey = $"reports/{job.CompanyId:N}/{job.Id:N}/{fileName}";
                await fileStorage.WriteAsync(storageKey, bytes, ct);
                job.Complete(storageKey, fileName, contentType, bytes.Length, timeProvider.GetUtcNow());
                await reportingRepository.SaveChangesAsync(ct);
                completed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                try { job.Fail(ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message, timeProvider.GetUtcNow()); await reportingRepository.SaveChangesAsync(ct); }
                catch { }
            }
        }
        return completed;
    }

    private async Task<ReportTable> BuildTableAsync(string reportType, ReportExportFilters filters, CancellationToken ct)
    {
        if (reportType == ReportTypes.CostLedger)
        {
            var rows = await financeRepository.ListCostLedgerAsync(true, [], filters.CompanyId, filters.ProjectId, null, null, null, filters.From, filters.To, 100_000, ct);
            return new ReportTable("Cost Ledger",
                ["Date","Source","Employee","Project","Cost Center","Category","Quantity","Unit","Amount","Currency","Allocation"],
                rows.Select(x => new[] { x.CostDate.ToString("yyyy-MM-dd"), x.SourceType, x.EmployeeNo ?? "", x.ProjectCode ?? "", x.CostCenterCode ?? "", x.Category, x.Quantity.ToString("0.####"), x.Unit, x.Amount.ToString("0.00"), x.Currency, x.AllocationBasis }).ToArray());
        }
        if (reportType == ReportTypes.Project360)
        {
            if (filters.ProjectId is null) throw new InvalidOperationException("Project id is required.");
            var p = await reportingRepository.GetProject360Async(filters.ProjectId.Value, filters.From, filters.To, ct) ?? throw new InvalidOperationException("Project not found.");
            var rows = new List<string[]>
            {
                ["Project", $"{p.ProjectCode} - {p.ProjectName}"], ["Range", $"{p.From:yyyy-MM-dd} / {p.To:yyyy-MM-dd}"], ["Headcount", p.Headcount.ToString()],
                ["Man-days", p.ManDays.ToString("0.####")], ["Worked hours", p.WorkedHours.ToString("0.00")], ["Approved OT hours", p.ApprovedOvertimeHours.ToString("0.00")],
                ["Meal quantity", p.MealQuantity.ToString("0.##")], ["Accommodation nights", p.AccommodationNights.ToString()]
            };
            foreach (var c in p.Costs) rows.Add([$"Cost {c.Currency}", $"Payroll={c.PayrollCost:0.00}; Meal={c.MealCost:0.00}; Accommodation={c.AccommodationCost:0.00}; Total={c.TotalCost:0.00}"]);
            return new ReportTable("Project 360", ["Metric","Value"], rows);
        }
        if (reportType == ReportTypes.MANAGEMENT) throw new InvalidOperationException("Invalid report type constant.");
        var management = await reportingRepository.ListManagementAsync(filters.CompanyId, filters.From, filters.To, ct);
        return new ReportTable("Management Dashboard",
            ["Project","Headcount","Man-days","Worked Hours","OT Hours","Meals","Nights","Costs"],
            management.Select(x => new[] { $"{x.ProjectCode} - {x.ProjectName}", x.Headcount.ToString(), x.ManDays.ToString("0.####"), x.WorkedHours.ToString("0.00"), x.ApprovedOvertimeHours.ToString("0.00"), x.MealQuantity.ToString("0.##"), x.AccommodationNights.ToString(), string.Join(" | ", x.Costs.Select(c => $"{c.Currency}:{c.TotalCost:0.00}")) }).ToArray());
    }
}

public sealed record ReportTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<string[]> Rows);

public static class ReportBinaryWriter
{
    public static byte[] WriteXlsx(ReportTable table)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            WriteEntry(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            WriteEntry(zip, "xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Report" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            var allRows = new List<IReadOnlyList<string>> { table.Headers }; allRows.AddRange(table.Rows);
            for (var r = 0; r < allRows.Count; r++)
            {
                sb.Append("<row r=\"").Append(r + 1).Append("\">");
                for (var c = 0; c < allRows[r].Count; c++)
                {
                    var value = SecurityElement.Escape(allRows[r][c] ?? string.Empty) ?? string.Empty;
                    sb.Append("<c r=\"").Append(Column(c + 1)).Append(r + 1).Append("\" t=\"inlineStr\"><is><t>").Append(value).Append("</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            WriteEntry(zip, "xl/worksheets/sheet1.xml", sb.ToString());
        }
        return output.ToArray();
    }

    public static byte[] WritePdf(ReportTable table)
    {
        var lines = new List<string> { table.Title, string.Join(" | ", table.Headers) };
        lines.AddRange(table.Rows.Select(r => string.Join(" | ", r)));
        const int perPage = 52;
        var pages = lines.Chunk(perPage).ToArray();
        var objectCount = 3 + pages.Length * 2;
        var fontId = objectCount;
        var objects = new Dictionary<int, byte[]>();
        var kids = string.Join(" ", Enumerable.Range(0, pages.Length).Select(i => $"{3 + i * 2} 0 R"));
        objects[1] = Bytes("<< /Type /Catalog /Pages 2 0 R >>");
        objects[2] = Bytes($"<< /Type /Pages /Kids [{kids}] /Count {pages.Length} >>");
        for (var i = 0; i < pages.Length; i++)
        {
            var pageId = 3 + i * 2; var contentId = pageId + 1;
            objects[pageId] = Bytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {contentId} 0 R >>");
            var content = new StringBuilder("BT /F1 8 Tf 36 806 Td ");
            foreach (var raw in pages[i]) content.Append('(').Append(PdfEscape(raw)).Append(") Tj 0 -14 Td ");
            content.Append("ET");
            var contentBytes = Bytes(content.ToString());
            objects[contentId] = Bytes($"<< /Length {contentBytes.Length} >>\nstream\n{Encoding.ASCII.GetString(contentBytes)}\nendstream");
        }
        objects[fontId] = Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        using var ms = new MemoryStream();
        Write(ms, "%PDF-1.4\n");
        var offsets = new long[fontId + 1];
        for (var id = 1; id <= fontId; id++)
        {
            offsets[id] = ms.Position; Write(ms, $"{id} 0 obj\n"); ms.Write(objects[id]); Write(ms, "\nendobj\n");
        }
        var xref = ms.Position; Write(ms, $"xref\n0 {fontId + 1}\n0000000000 65535 f \n");
        for (var id = 1; id <= fontId; id++) Write(ms, $"{offsets[id]:D10} 00000 n \n");
        Write(ms, $"trailer\n<< /Size {fontId + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    private static string Column(int index) { var s = string.Empty; while (index > 0) { index--; s = (char)('A' + index % 26) + s; index /= 26; } return s; }
    private static void WriteEntry(ZipArchive zip, string path, string content) { var entry = zip.CreateEntry(path, CompressionLevel.Fastest); using var stream = entry.Open(); var bytes = Encoding.UTF8.GetBytes(content); stream.Write(bytes); }
    private static byte[] Bytes(string value) => Encoding.ASCII.GetBytes(value);
    private static void Write(Stream stream, string value) { var bytes = Bytes(value); stream.Write(bytes); }
    private static string PdfEscape(string value)
    {
        var ascii = new string(value.Select(ch => ch is >= ' ' and <= '~' ? ch : '?').ToArray());
        return ascii.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    }
}
