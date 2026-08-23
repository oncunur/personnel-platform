using System.Net.Mime;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonnelPlatform.Api.Contracts;

namespace PersonnelPlatform.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { logger.LogInformation("Request {TraceId} was cancelled by the client.", context.TraceIdentifier); }
        catch (DbUpdateException ex) when (IsRequiredLeaveAttachment(ex.InnerException)) { await WriteRequiredLeaveAttachmentAsync(context); }
        catch (PostgresException ex) when (IsRequiredLeaveAttachment(ex)) { await WriteRequiredLeaveAttachmentAsync(context); }
        catch (DbUpdateException ex) when (TryMapBusinessConstraint(ex.InnerException, out _, out _)) { var p = (PostgresException)ex.InnerException!; TryMapBusinessConstraint(p, out var code, out var message); await WriteConflictAsync(context, code, message, p.ConstraintName); }
        catch (PostgresException ex) when (TryMapBusinessConstraint(ex, out _, out _)) { TryMapBusinessConstraint(ex, out var code, out var message); await WriteConflictAsync(context, code, message, ex.ConstraintName); }
        catch (DbUpdateException ex) when (TryMapBusinessSignal(ex.InnerException, out _, out _)) { var p = (PostgresException)ex.InnerException!; TryMapBusinessSignal(p, out var code, out var message); await WriteConflictAsync(context, code, message, p.MessageText); }
        catch (PostgresException ex) when (TryMapBusinessSignal(ex, out _, out _)) { TryMapBusinessSignal(ex, out var code, out var message); await WriteConflictAsync(context, code, message, ex.MessageText); }
        catch (DbUpdateConcurrencyException ex) { logger.LogInformation(ex, "Optimistic concurrency conflict. TraceId={TraceId}", context.TraceIdentifier); await WriteConflictAsync(context, "RECORD_MODIFIED_BY_ANOTHER_USER", "Kayıt başka bir işlem tarafından değiştirildi. Veriyi yenileyip tekrar deneyin.", null); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.Clear(); context.Response.StatusCode = StatusCodes.Status500InternalServerError; context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(ApiErrorResponse.Create("UNEXPECTED_ERROR", "Beklenmeyen bir sistem hatası oluştu.", context.TraceIdentifier), context.RequestAborted);
        }
    }

    private async Task WriteRequiredLeaveAttachmentAsync(HttpContext context)
    {
        logger.LogInformation("Leave submit rejected because a required attachment is missing. TraceId={TraceId}", context.TraceIdentifier);
        if (context.Response.HasStarted) throw new InvalidOperationException("Response already started while handling leave attachment requirement.");
        context.Response.Clear(); context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity; context.Response.ContentType = MediaTypeNames.Application.Json;
        await context.Response.WriteAsJsonAsync(ApiErrorResponse.Create("LEAVE_ATTACHMENT_REQUIRED", "Bu izin türü gönderilmeden önce destekleyici belge yüklenmelidir.", context.TraceIdentifier), context.RequestAborted);
    }

    private async Task WriteConflictAsync(HttpContext context, string code, string message, string? rule)
    {
        logger.LogInformation("Database business rule rejected request. Rule={Rule} Code={ErrorCode} TraceId={TraceId}", rule, code, context.TraceIdentifier);
        if (context.Response.HasStarted) throw new InvalidOperationException("Response already started while handling a database business rule.");
        context.Response.Clear(); context.Response.StatusCode = StatusCodes.Status409Conflict; context.Response.ContentType = MediaTypeNames.Application.Json;
        await context.Response.WriteAsJsonAsync(ApiErrorResponse.Create(code, message, context.TraceIdentifier), context.RequestAborted);
    }

    private static bool IsRequiredLeaveAttachment(Exception? exception) => exception is PostgresException p && p.SqlState == "P0001" && p.MessageText == "LEAVE_ATTACHMENT_REQUIRED";

    private static bool TryMapBusinessSignal(Exception? exception, out string code, out string message)
    {
        code = string.Empty; message = string.Empty;
        if (exception is not PostgresException p || p.SqlState != "P0001") return false;
        switch (p.MessageText)
        {
            case "RAW_ATTENDANCE_IMMUTABLE": code = "RAW_ATTENDANCE_IMMUTABLE"; message = "Ham PDKS kayıtları değiştirilemez veya silinemez."; return true;
            case "PAYROLL_CLOSED_IMMUTABLE": code = "PAYROLL_CLOSED_IMMUTABLE"; message = "Kapatılmış bordro dönemi ve sonuçları değiştirilemez. Düzeltme için yeni revision oluşturun."; return true;
            case "STOCK_NEGATIVE_NOT_ALLOWED": code = "STOCK_NEGATIVE_NOT_ALLOWED"; message = "Stok bakiyesi negatife düşürülemez."; return true;
            case "STOCK_MOVEMENT_IMMUTABLE": code = "STOCK_MOVEMENT_IMMUTABLE"; message = "Stok hareketleri değiştirilemez veya silinemez. Düzeltme için yeni ters/düzeltme hareketi oluşturun."; return true;
            case "VEHICLE_ODOMETER_REGRESSION": code = "VEHICLE_ODOMETER_REGRESSION"; message = "Araç kilometre değeri son kayıtlı değerden küçük olamaz."; return true;
            case "VEHICLE_LEDGER_IMMUTABLE": code = "VEHICLE_LEDGER_IMMUTABLE"; message = "Araç kilometre, bakım ve yakıt defteri kayıtları değiştirilemez veya silinemez. Düzeltme yeni kayıtla yapılmalıdır."; return true;
            case "ADMIN_HISTORY_IMMUTABLE": code = "ADMIN_HISTORY_IMMUTABLE"; message = "İdari görev tamamlama ve reminder event geçmişi değiştirilemez veya silinemez."; return true;
            default: return false;
        }
    }

    private static bool TryMapBusinessConstraint(Exception? exception, out string code, out string message)
    {
        code = string.Empty; message = string.Empty; if (exception is not PostgresException p) return false;
        switch (p.ConstraintName)
        {
            case "ex_employee_shift_assignments_overlap" when p.SqlState == PostgresErrorCodes.ExclusionViolation: code = "SHIFT_ASSIGNMENT_DATE_CONFLICT"; message = "Personelin bu tarih aralığında başka bir vardiya ataması bulunuyor."; return true;
            case "ux_work_calendars_default_company" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "DEFAULT_WORK_CALENDAR_EXISTS"; message = "Bu şirket için zaten varsayılan çalışma takvimi bulunuyor."; return true;
            case "ux_work_calendars_company_code" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "WORK_CALENDAR_CODE_EXISTS"; message = "Bu çalışma takvimi kodu zaten kullanılıyor."; return true;
            case "ux_shifts_company_code" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "SHIFT_CODE_EXISTS"; message = "Bu vardiya kodu zaten kullanılıyor."; return true;
            case "ux_work_calendar_days_calendar_date" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "WORK_CALENDAR_DAY_CONFLICT"; message = "Bu tarih için takvim günü başka bir işlem tarafından oluşturuldu; veriyi yenileyip tekrar deneyin."; return true;
            case "ux_raw_attendance_events_company_source_external" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "RAW_ATTENDANCE_EVENT_DUPLICATE"; message = "Aynı harici PDKS olayı daha önce alınmış. Kayıt yinelenmedi."; return true;
            case "ux_daily_attendance_employee_date" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "DAILY_ATTENDANCE_CONCURRENT_UPDATE"; message = "Bu günün puantajı başka bir işlem tarafından oluşturuldu. Veriyi yenileyip tekrar deneyin."; return true;
            case "ux_overtime_active_daily" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "OVERTIME_REQUEST_ALREADY_EXISTS"; message = "Bu günlük puantaj için açık veya onaylanmış bir fazla mesai talebi zaten bulunuyor."; return true;
            case "ux_camps_company_code" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "CAMP_CODE_EXISTS"; message = "Bu şirket için kamp kodu zaten kullanılıyor."; return true;
            case "ux_camp_rooms_camp_code" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "CAMP_ROOM_CODE_EXISTS"; message = "Bu kamp içinde oda kodu zaten kullanılıyor."; return true;
            case "ux_camp_beds_room_code" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "CAMP_BED_CODE_EXISTS"; message = "Bu oda içinde yatak kodu zaten kullanılıyor."; return true;
            case "ex_accommodation_rates_overlap" when p.SqlState == PostgresErrorCodes.ExclusionViolation: code = "CAMP_RATE_DATE_CONFLICT"; message = "Bu kamp için tarih aralığı çakışan başka bir konaklama fiyatı bulunuyor."; return true;
            case "ex_accommodation_stays_bed_overlap" when p.SqlState == PostgresErrorCodes.ExclusionViolation: code = "CAMP_BED_OCCUPANCY_CONFLICT"; message = "Seçilen yatak bu tarih aralığında başka bir personel tarafından kullanılıyor."; return true;
            case "ex_accommodation_stays_employee_overlap" when p.SqlState == PostgresErrorCodes.ExclusionViolation: code = "CAMP_EMPLOYEE_ACCOMMODATION_CONFLICT"; message = "Personelin bu tarih aralığında başka bir konaklama kaydı bulunuyor."; return true;
            case "ex_meal_rates_overlap" when p.SqlState == PostgresErrorCodes.ExclusionViolation: code = "MEAL_RATE_DATE_CONFLICT"; message = "Bu kamp ve öğün türü için tarih aralığı çakışan başka bir fiyat bulunuyor."; return true;
            case "ux_meal_consumptions_employee_date_type" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "MEAL_ALREADY_CONSUMED"; message = "Personel için bu gün ve öğün türünde tüketim kaydı zaten bulunuyor."; return true;
            case "ux_meal_consumptions_company_source_external" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "MEAL_EXTERNAL_EVENT_DUPLICATE"; message = "Aynı harici yemek olayı daha önce kaydedilmiş. Kayıt yinelenmedi."; return true;
            case "ex_employee_compensations_overlap" when p.SqlState == PostgresErrorCodes.ExclusionViolation: code = "PAYROLL_COMPENSATION_DATE_CONFLICT"; message = "Personelin bu tarih aralığında başka bir ücret tanımı bulunuyor."; return true;
            case "ux_payroll_period_revision" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "PAYROLL_PERIOD_REVISION_EXISTS"; message = "Bu ay ve revision için bordro dönemi başka bir işlem tarafından oluşturuldu."; return true;
            case "ux_payroll_results_period_employee" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "PAYROLL_RESULT_ALREADY_EXISTS"; message = "Bu bordro dönemi için personel sonucu zaten oluşturulmuş. Veriyi yenileyin."; return true;
            case "ux_stock_locations_company_code" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "STOCK_LOCATION_CODE_EXISTS"; message = "Bu şirket için stok lokasyonu kodu zaten kullanılıyor."; return true;
            case "ux_stock_items_company_code" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "STOCK_ITEM_CODE_EXISTS"; message = "Bu şirket için stok kalemi kodu zaten kullanılıyor."; return true;
            case "ux_stock_movements_company_source_external" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "STOCK_EXTERNAL_EVENT_DUPLICATE"; message = "Aynı harici stok hareketi daha önce kaydedilmiş. Kayıt yinelenmedi."; return true;
            case "ux_assets_company_tag" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "ASSET_TAG_EXISTS"; message = "Bu şirket için demirbaş etiketi zaten kullanılıyor."; return true;
            case "ux_assets_company_serial" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "ASSET_SERIAL_EXISTS"; message = "Bu şirket için seri numarası zaten kullanılıyor."; return true;
            case "ux_asset_assignments_active_asset" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "ASSET_ALREADY_ASSIGNED"; message = "Demirbaşın aktif zimmeti zaten bulunuyor."; return true;
            case "ux_vehicles_company_plate" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "VEHICLE_PLATE_EXISTS"; message = "Bu şirket için plaka zaten kayıtlı."; return true;
            case "ux_vehicles_company_vin" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "VEHICLE_VIN_EXISTS"; message = "Bu şirket için VIN/şasi numarası zaten kayıtlı."; return true;
            case "ex_vehicle_assignments_overlap" when p.SqlState == PostgresErrorCodes.ExclusionViolation: code = "VEHICLE_ASSIGNMENT_DATE_CONFLICT"; message = "Araç için tarih aralığı çakışan başka bir personel ataması bulunuyor."; return true;
            case "ux_vehicle_odometer_company_source_external" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "VEHICLE_EXTERNAL_EVENT_DUPLICATE"; message = "Aynı harici kilometre olayı daha önce kaydedilmiş."; return true;
            case "ux_vehicle_fuel_company_source_external" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "VEHICLE_FUEL_EXTERNAL_EVENT_DUPLICATE"; message = "Aynı harici yakıt kaydı daha önce işlenmiş."; return true;
            case "ux_administrative_tasks_company_code" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "ADMIN_TASK_CODE_EXISTS"; message = "Bu şirket için idari görev kodu zaten kullanılıyor."; return true;
            case "ux_administrative_contracts_company_no" when p.SqlState == PostgresErrorCodes.UniqueViolation: code = "ADMIN_CONTRACT_NO_EXISTS"; message = "Bu şirket için kontrat numarası zaten kullanılıyor."; return true;
            default: return false;
        }
    }
}
