using System.Net.Mime;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonnelPlatform.Api.Contracts;

namespace PersonnelPlatform.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {TraceId} was cancelled by the client.", context.TraceIdentifier);
        }
        catch (DbUpdateException exception) when (IsRequiredLeaveAttachment(exception.InnerException))
        {
            await WriteRequiredLeaveAttachmentAsync(context);
        }
        catch (PostgresException exception) when (IsRequiredLeaveAttachment(exception))
        {
            await WriteRequiredLeaveAttachmentAsync(context);
        }
        catch (DbUpdateException exception) when (TryMapBusinessConstraint(exception.InnerException, out _, out _))
        {
            var postgres = (PostgresException)exception.InnerException!;
            TryMapBusinessConstraint(postgres, out var code, out var message);
            await WriteConflictAsync(context, code, message, postgres.ConstraintName);
        }
        catch (PostgresException exception) when (TryMapBusinessConstraint(exception, out _, out _))
        {
            TryMapBusinessConstraint(exception, out var code, out var message);
            await WriteConflictAsync(context, code, message, exception.ConstraintName);
        }
        catch (DbUpdateException exception) when (IsRawAttendanceImmutable(exception.InnerException))
        {
            await WriteConflictAsync(context, "RAW_ATTENDANCE_IMMUTABLE", "Ham PDKS kayıtları değiştirilemez veya silinemez.", "trg_raw_events_immutable");
        }
        catch (PostgresException exception) when (IsRawAttendanceImmutable(exception))
        {
            await WriteConflictAsync(context, "RAW_ATTENDANCE_IMMUTABLE", "Ham PDKS kayıtları değiştirilemez veya silinemez.", "trg_raw_events_immutable");
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogInformation(exception, "Optimistic concurrency conflict. TraceId={TraceId}", context.TraceIdentifier);
            await WriteConflictAsync(context, "RECORD_MODIFIED_BY_ANOTHER_USER", "Kayıt başka bir işlem tarafından değiştirildi. Veriyi yenileyip tekrar deneyin.", null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", context.TraceIdentifier);

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(
                ApiErrorResponse.Create("UNEXPECTED_ERROR", "Beklenmeyen bir sistem hatası oluştu.", context.TraceIdentifier),
                context.RequestAborted);
        }
    }

    private async Task WriteRequiredLeaveAttachmentAsync(HttpContext context)
    {
        logger.LogInformation("Leave submit rejected because a required attachment is missing. TraceId={TraceId}", context.TraceIdentifier);
        if (context.Response.HasStarted) throw new InvalidOperationException("Response already started while handling leave attachment requirement.");
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = MediaTypeNames.Application.Json;
        await context.Response.WriteAsJsonAsync(
            ApiErrorResponse.Create("LEAVE_ATTACHMENT_REQUIRED", "Bu izin türü gönderilmeden önce destekleyici belge yüklenmelidir.", context.TraceIdentifier),
            context.RequestAborted);
    }

    private async Task WriteConflictAsync(HttpContext context, string code, string message, string? constraintName)
    {
        logger.LogInformation("Database business constraint rejected request. Constraint={ConstraintName} Code={ErrorCode} TraceId={TraceId}", constraintName, code, context.TraceIdentifier);
        if (context.Response.HasStarted) throw new InvalidOperationException("Response already started while handling a database business constraint.");
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        context.Response.ContentType = MediaTypeNames.Application.Json;
        await context.Response.WriteAsJsonAsync(ApiErrorResponse.Create(code, message, context.TraceIdentifier), context.RequestAborted);
    }

    private static bool IsRequiredLeaveAttachment(Exception? exception) =>
        exception is PostgresException postgres && postgres.SqlState == "P0001" && postgres.MessageText == "LEAVE_ATTACHMENT_REQUIRED";

    private static bool IsRawAttendanceImmutable(Exception? exception) =>
        exception is PostgresException postgres && postgres.SqlState == "P0001" && postgres.MessageText == "RAW_ATTENDANCE_IMMUTABLE";

    private static bool TryMapBusinessConstraint(Exception? exception, out string code, out string message)
    {
        code = string.Empty;
        message = string.Empty;
        if (exception is not PostgresException postgres) return false;

        switch (postgres.ConstraintName)
        {
            case "ex_employee_shift_assignments_overlap" when postgres.SqlState == PostgresErrorCodes.ExclusionViolation:
                code = "SHIFT_ASSIGNMENT_DATE_CONFLICT";
                message = "Personelin bu tarih aralığında başka bir vardiya ataması bulunuyor.";
                return true;
            case "ux_work_calendars_default_company" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "DEFAULT_WORK_CALENDAR_EXISTS";
                message = "Bu şirket için zaten varsayılan çalışma takvimi bulunuyor.";
                return true;
            case "ux_work_calendars_company_code" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "WORK_CALENDAR_CODE_EXISTS";
                message = "Bu çalışma takvimi kodu zaten kullanılıyor.";
                return true;
            case "ux_shifts_company_code" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "SHIFT_CODE_EXISTS";
                message = "Bu vardiya kodu zaten kullanılıyor.";
                return true;
            case "ux_work_calendar_days_calendar_date" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "WORK_CALENDAR_DAY_CONFLICT";
                message = "Bu tarih için takvim günü başka bir işlem tarafından oluşturuldu; veriyi yenileyip tekrar deneyin.";
                return true;
            case "ux_raw_attendance_events_company_source_external" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "RAW_ATTENDANCE_EVENT_DUPLICATE";
                message = "Aynı harici PDKS olayı daha önce alınmış. Kayıt yinelenmedi.";
                return true;
            case "ux_daily_attendance_employee_date" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "DAILY_ATTENDANCE_CONCURRENT_UPDATE";
                message = "Bu günün puantajı başka bir işlem tarafından oluşturuldu. Veriyi yenileyip tekrar deneyin.";
                return true;
            case "ux_overtime_active_daily" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "OVERTIME_REQUEST_ALREADY_EXISTS";
                message = "Bu günlük puantaj için açık veya onaylanmış bir fazla mesai talebi zaten bulunuyor.";
                return true;
            case "ux_camps_company_code" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "CAMP_CODE_EXISTS";
                message = "Bu şirket için kamp kodu zaten kullanılıyor.";
                return true;
            case "ux_camp_rooms_camp_code" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "CAMP_ROOM_CODE_EXISTS";
                message = "Bu kamp içinde oda kodu zaten kullanılıyor.";
                return true;
            case "ux_camp_beds_room_code" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "CAMP_BED_CODE_EXISTS";
                message = "Bu oda içinde yatak kodu zaten kullanılıyor.";
                return true;
            case "ex_accommodation_rates_overlap" when postgres.SqlState == PostgresErrorCodes.ExclusionViolation:
                code = "CAMP_RATE_DATE_CONFLICT";
                message = "Bu kamp için tarih aralığı çakışan başka bir konaklama fiyatı bulunuyor.";
                return true;
            case "ex_accommodation_stays_bed_overlap" when postgres.SqlState == PostgresErrorCodes.ExclusionViolation:
                code = "CAMP_BED_OCCUPANCY_CONFLICT";
                message = "Seçilen yatak bu tarih aralığında başka bir personel tarafından kullanılıyor.";
                return true;
            case "ex_accommodation_stays_employee_overlap" when postgres.SqlState == PostgresErrorCodes.ExclusionViolation:
                code = "CAMP_EMPLOYEE_ACCOMMODATION_CONFLICT";
                message = "Personelin bu tarih aralığında başka bir konaklama kaydı bulunuyor.";
                return true;
            case "ex_meal_rates_overlap" when postgres.SqlState == PostgresErrorCodes.ExclusionViolation:
                code = "MEAL_RATE_DATE_CONFLICT";
                message = "Bu kamp ve öğün türü için tarih aralığı çakışan başka bir fiyat bulunuyor.";
                return true;
            case "ux_meal_consumptions_employee_date_type" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "MEAL_ALREADY_CONSUMED";
                message = "Personel için bu gün ve öğün türünde tüketim kaydı zaten bulunuyor.";
                return true;
            case "ux_meal_consumptions_company_source_external" when postgres.SqlState == PostgresErrorCodes.UniqueViolation:
                code = "MEAL_EXTERNAL_EVENT_DUPLICATE";
                message = "Aynı harici yemek olayı daha önce kaydedilmiş. Kayıt yinelenmedi.";
                return true;
            default:
                return false;
        }
    }
}
