using Microsoft.EntityFrameworkCore;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Attendance;
using PersonnelPlatform.Domain.Audit;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Leave;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Payroll;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserScope> UserScopes => Set<UserScope>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    public DbSet<EmployeeType> EmployeeTypes => Set<EmployeeType>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeProjectAssignment> EmployeeProjectAssignments => Set<EmployeeProjectAssignment>();
    public DbSet<EmployeeUserLink> EmployeeUserLinks => Set<EmployeeUserLink>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentTypeEmployeeTypeRequirement> DocumentTypeEmployeeTypeRequirements => Set<DocumentTypeEmployeeTypeRequirement>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeDocumentHistory> EmployeeDocumentHistories => Set<EmployeeDocumentHistory>();

    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveEntitlement> LeaveEntitlements => Set<LeaveEntitlement>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveApproval> LeaveApprovals => Set<LeaveApproval>();
    public DbSet<LeaveApprovalHistory> LeaveApprovalHistories => Set<LeaveApprovalHistory>();
    public DbSet<LeaveAttachment> LeaveAttachments => Set<LeaveAttachment>();

    public DbSet<WorkCalendar> WorkCalendars => Set<WorkCalendar>();
    public DbSet<WorkCalendarDay> WorkCalendarDays => Set<WorkCalendarDay>();
    public DbSet<ShiftDefinition> Shifts => Set<ShiftDefinition>();
    public DbSet<EmployeeShiftAssignment> EmployeeShiftAssignments => Set<EmployeeShiftAssignment>();
    public DbSet<RawAttendanceEvent> RawAttendanceEvents => Set<RawAttendanceEvent>();
    public DbSet<DailyAttendance> DailyAttendances => Set<DailyAttendance>();
    public DbSet<OvertimeRequest> OvertimeRequests => Set<OvertimeRequest>();

    public DbSet<CampSite> Camps => Set<CampSite>();
    public DbSet<CampRoom> CampRooms => Set<CampRoom>();
    public DbSet<CampBed> CampBeds => Set<CampBed>();
    public DbSet<AccommodationRate> AccommodationRates => Set<AccommodationRate>();
    public DbSet<AccommodationStay> AccommodationStays => Set<AccommodationStay>();

    public DbSet<MealType> MealTypes => Set<MealType>();
    public DbSet<MealRate> MealRates => Set<MealRate>();
    public DbSet<MealConsumption> MealConsumptions => Set<MealConsumption>();

    public DbSet<EmployeeCompensation> EmployeeCompensations => Set<EmployeeCompensation>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollEmployeeResult> PayrollEmployeeResults => Set<PayrollEmployeeResult>();

    public DbSet<StockLocation> StockLocations => Set<StockLocation>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<AssetItem> Assets => Set<AssetItem>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleAssignment> VehicleAssignments => Set<VehicleAssignment>();
    public DbSet<VehicleOdometerEvent> VehicleOdometerEvents => Set<VehicleOdometerEvent>();
    public DbSet<VehicleMaintenanceRecord> VehicleMaintenanceRecords => Set<VehicleMaintenanceRecord>();
    public DbSet<VehicleFuelRecord> VehicleFuelRecords => Set<VehicleFuelRecord>();

    public DbSet<AdministrativeTask> AdministrativeTasks => Set<AdministrativeTask>();
    public DbSet<AdministrativeTaskCompletion> AdministrativeTaskCompletions => Set<AdministrativeTaskCompletion>();
    public DbSet<AdministrativeContract> AdministrativeContracts => Set<AdministrativeContract>();
    public DbSet<AdministrativeReminderEvent> AdministrativeReminderEvents => Set<AdministrativeReminderEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
