using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_vehicles");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Plate).HasColumnName("plate").HasMaxLength(30);
        builder.Property(x => x.Vin).HasColumnName("vin").HasMaxLength(50);
        builder.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(100);
        builder.Property(x => x.Model).HasColumnName("model").HasMaxLength(100);
        builder.Property(x => x.ModelYear).HasColumnName("model_year");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.InsuranceValidUntil).HasColumnName("insurance_valid_until");
        builder.Property(x => x.InspectionValidUntil).HasColumnName("inspection_valid_until");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicles_company");
        builder.HasIndex(x => new { x.CompanyId, x.Plate }).IsUnique().HasDatabaseName("ux_vehicles_company_plate");
        builder.HasIndex(x => new { x.CompanyId, x.Vin }).HasDatabaseName("ix_vehicles_company_vin_model");
    }
}

public sealed class VehicleAssignmentConfiguration : IEntityTypeConfiguration<VehicleAssignment>
{
    public void Configure(EntityTypeBuilder<VehicleAssignment> builder)
    {
        builder.ToTable("vehicle_assignments", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_vehicle_assignments");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.ProjectIdSnapshot).HasColumnName("project_id_snapshot");
        builder.Property(x => x.CostCenterIdSnapshot).HasColumnName("cost_center_id_snapshot");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntilExclusive).HasColumnName("valid_until_exclusive");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_assignments_vehicle");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_assignments_employee");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_assignments_project_snapshot");
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_assignments_cost_center_snapshot");
        builder.HasIndex(x => new { x.VehicleId, x.ValidFrom }).HasDatabaseName("ix_vehicle_assignments_vehicle_date");
        builder.HasIndex(x => new { x.EmployeeId, x.ValidFrom }).HasDatabaseName("ix_vehicle_assignments_employee_date");
    }
}

public sealed class VehicleOdometerEventConfiguration : IEntityTypeConfiguration<VehicleOdometerEvent>
{
    public void Configure(EntityTypeBuilder<VehicleOdometerEvent> builder)
    {
        builder.ToTable("vehicle_odometer_events", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_vehicle_odometer_events");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        builder.Property(x => x.OdometerKm).HasColumnName("odometer_km");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(20);
        builder.Property(x => x.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(200);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_odometer_vehicle");
        builder.HasIndex(x => new { x.VehicleId, x.OccurredAt }).HasDatabaseName("ix_vehicle_odometer_vehicle_time");
        builder.HasIndex(x => new { x.CompanyId, x.Source, x.ExternalEventId }).HasDatabaseName("ix_vehicle_odometer_external_model");
    }
}

public sealed class VehicleMaintenanceRecordConfiguration : IEntityTypeConfiguration<VehicleMaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<VehicleMaintenanceRecord> builder)
    {
        builder.ToTable("vehicle_maintenance", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_vehicle_maintenance");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        builder.Property(x => x.OdometerEventId).HasColumnName("odometer_event_id");
        builder.Property(x => x.MaintenanceType).HasColumnName("maintenance_type").HasMaxLength(80);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(x => x.Cost).HasColumnName("cost").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.ServiceDate).HasColumnName("service_date");
        builder.Property(x => x.NextDueDate).HasColumnName("next_due_date");
        builder.Property(x => x.NextDueOdometerKm).HasColumnName("next_due_odometer_km");
        builder.Property(x => x.Vendor).HasColumnName("vendor").HasMaxLength(200);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_maintenance_vehicle");
        builder.HasOne<VehicleOdometerEvent>().WithMany().HasForeignKey(x => x.OdometerEventId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_maintenance_odometer");
        builder.HasIndex(x => new { x.VehicleId, x.ServiceDate }).HasDatabaseName("ix_vehicle_maintenance_vehicle_date");
    }
}

public sealed class VehicleFuelRecordConfiguration : IEntityTypeConfiguration<VehicleFuelRecord>
{
    public void Configure(EntityTypeBuilder<VehicleFuelRecord> builder)
    {
        builder.ToTable("vehicle_fuel", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_vehicle_fuel");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        builder.Property(x => x.OdometerEventId).HasColumnName("odometer_event_id");
        builder.Property(x => x.Liters).HasColumnName("liters").HasPrecision(18, 3);
        builder.Property(x => x.TotalCost).HasColumnName("total_cost").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.FueledAt).HasColumnName("fueled_at");
        builder.Property(x => x.Station).HasColumnName("station").HasMaxLength(200);
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(20);
        builder.Property(x => x.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(200);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_fuel_vehicle");
        builder.HasOne<VehicleOdometerEvent>().WithMany().HasForeignKey(x => x.OdometerEventId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vehicle_fuel_odometer");
        builder.HasIndex(x => new { x.VehicleId, x.FueledAt }).HasDatabaseName("ix_vehicle_fuel_vehicle_time");
        builder.HasIndex(x => new { x.CompanyId, x.Source, x.ExternalEventId }).HasDatabaseName("ix_vehicle_fuel_external_model");
    }
}
