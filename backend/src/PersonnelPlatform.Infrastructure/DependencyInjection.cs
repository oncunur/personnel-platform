using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonnelPlatform.Application.Administration;
using PersonnelPlatform.Application.Attendance;
using PersonnelPlatform.Application.Audit;
using PersonnelPlatform.Application.Camp;
using PersonnelPlatform.Application.Documents;
using PersonnelPlatform.Application.Finance;
using PersonnelPlatform.Application.Integration;
using PersonnelPlatform.Application.Leave;
using PersonnelPlatform.Application.Meal;
using PersonnelPlatform.Application.Migration;
using PersonnelPlatform.Application.Notification;
using PersonnelPlatform.Application.Organization;
using PersonnelPlatform.Application.Payroll;
using PersonnelPlatform.Application.Personnel;
using PersonnelPlatform.Application.Reporting;
using PersonnelPlatform.Application.Security;
using PersonnelPlatform.Application.Workflow;
using PersonnelPlatform.Infrastructure.Administration;
using PersonnelPlatform.Infrastructure.Attendance;
using PersonnelPlatform.Infrastructure.Audit;
using PersonnelPlatform.Infrastructure.Camp;
using PersonnelPlatform.Infrastructure.Documents;
using PersonnelPlatform.Infrastructure.Finance;
using PersonnelPlatform.Infrastructure.Health;
using PersonnelPlatform.Infrastructure.Integration;
using PersonnelPlatform.Infrastructure.Leave;
using PersonnelPlatform.Infrastructure.Meal;
using PersonnelPlatform.Infrastructure.Migration;
using PersonnelPlatform.Infrastructure.Notification;
using PersonnelPlatform.Infrastructure.Organization;
using PersonnelPlatform.Infrastructure.Payroll;
using PersonnelPlatform.Infrastructure.Personnel;
using PersonnelPlatform.Infrastructure.Persistence;
using PersonnelPlatform.Infrastructure.Reporting;
using PersonnelPlatform.Infrastructure.Security;
using PersonnelPlatform.Infrastructure.Workflow;

namespace PersonnelPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", DatabaseSchemas.System)));

        var dataProtectionKey = configuration["Security:DataProtectionKey"];
        if (string.IsNullOrWhiteSpace(dataProtectionKey)) throw new InvalidOperationException("Configuration value 'Security:DataProtectionKey' is required.");
        services.AddSingleton<ISensitiveDataProtector>(new AesGcmSensitiveDataProtector(dataProtectionKey));

        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<AuditService>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IPersonnelRepository, PersonnelRepository>();
        services.AddScoped<IEmployeeSensitiveRepository, EmployeeSensitiveRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentHistoryRepository, DocumentHistoryRepository>();
        services.AddScoped<ILeaveRepository, LeaveRepository>();
        services.AddScoped<ILeaveApprovalRepository, LeaveApprovalRepository>();
        services.AddScoped<ILeaveAttachmentRepository, LeaveAttachmentRepository>();
        services.AddScoped<IAttendanceSetupRepository, AttendanceSetupRepository>();
        services.AddScoped<IAttendanceProcessingRepository, AttendanceProcessingRepository>();
        services.AddScoped<IOvertimeRepository, OvertimeRepository>();
        services.AddScoped<ICampRepository, CampRepository>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<IPayrollRepository, PayrollRepository>();
        services.AddScoped<ISalaryProtectionRepository, SalaryProtectionRepository>();
        services.AddScoped<IAssetStockRepository, AssetStockRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IAdministrativeAffairsRepository, AdministrativeAffairsRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IFinanceRepository, FinanceRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<IImportErpRepository, ImportErpRepository>();
        services.AddScoped<IMigrationRepository, MigrationRepository>();
        services.AddScoped<MigrationService>();

        var storageRoot = configuration["FileStorage:RootPath"];
        if (string.IsNullOrWhiteSpace(storageRoot)) storageRoot = Path.Combine(AppContext.BaseDirectory, "storage");
        services.AddSingleton(new LocalFileStorageOptions(storageRoot));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IReportFileStorage, ReportFileStorage>();
        var maxMbRaw = configuration["FileStorage:MaxUploadSizeMb"];
        var maxMb = long.TryParse(maxMbRaw, out var parsedMaxMb) && parsedMaxMb > 0 ? parsedMaxMb : 10;
        services.AddSingleton(new DocumentFilePolicyOptions(maxMb * 1024 * 1024));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<RedisTcpHealthCheck>();
        services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>(name: "postgres", tags: ["ready"]).AddCheck<RedisTcpHealthCheck>(name: "redis", tags: ["ready"]);
        return services;
    }
}