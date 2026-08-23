using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Common;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class CampConfigurationHelpers
{
    public static void ConfigureAudit<T>(EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
    }
}

public sealed class CampSiteConfiguration : IEntityTypeConfiguration<CampSite>
{
    public void Configure(EntityTypeBuilder<CampSite> builder)
    {
        builder.ToTable("camps", DatabaseSchemas.Camp);
        builder.HasKey(x => x.Id).HasName("pk_camps");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(1000);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        CampConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_camps_company");
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_camps_company_code");
    }
}

public sealed class CampRoomConfiguration : IEntityTypeConfiguration<CampRoom>
{
    public void Configure(EntityTypeBuilder<CampRoom> builder)
    {
        builder.ToTable("rooms", DatabaseSchemas.Camp);
        builder.HasKey(x => x.Id).HasName("pk_camp_rooms");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CampId).HasColumnName("camp_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Floor).HasColumnName("floor");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        CampConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<CampSite>().WithMany().HasForeignKey(x => x.CampId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_camp_rooms_camp");
        builder.HasIndex(x => new { x.CampId, x.Code }).IsUnique().HasDatabaseName("ux_camp_rooms_camp_code");
    }
}

public sealed class CampBedConfiguration : IEntityTypeConfiguration<CampBed>
{
    public void Configure(EntityTypeBuilder<CampBed> builder)
    {
        builder.ToTable("beds", DatabaseSchemas.Camp);
        builder.HasKey(x => x.Id).HasName("pk_camp_beds");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RoomId).HasColumnName("room_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        CampConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<CampRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_camp_beds_room");
        builder.HasIndex(x => new { x.RoomId, x.Code }).IsUnique().HasDatabaseName("ux_camp_beds_room_code");
    }
}

public sealed class AccommodationRateConfiguration : IEntityTypeConfiguration<AccommodationRate>
{
    public void Configure(EntityTypeBuilder<AccommodationRate> builder)
    {
        builder.ToTable("accommodation_rates", DatabaseSchemas.Camp);
        builder.HasKey(x => x.Id).HasName("pk_accommodation_rates");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CampId).HasColumnName("camp_id");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntilExclusive).HasColumnName("valid_until_exclusive");
        builder.Property(x => x.NightlyRate).HasColumnName("nightly_rate").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        CampConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<CampSite>().WithMany().HasForeignKey(x => x.CampId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_rates_camp");
        builder.HasIndex(x => new { x.CampId, x.ValidFrom }).HasDatabaseName("ix_accommodation_rates_camp_from");
    }
}

public sealed class AccommodationStayConfiguration : IEntityTypeConfiguration<AccommodationStay>
{
    public void Configure(EntityTypeBuilder<AccommodationStay> builder)
    {
        builder.ToTable("accommodation_stays", DatabaseSchemas.Camp);
        builder.HasKey(x => x.Id).HasName("pk_accommodation_stays");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.CampId).HasColumnName("camp_id");
        builder.Property(x => x.RoomId).HasColumnName("room_id");
        builder.Property(x => x.BedId).HasColumnName("bed_id");
        builder.Property(x => x.RateId).HasColumnName("rate_id");
        builder.Property(x => x.ProjectIdSnapshot).HasColumnName("project_id_snapshot");
        builder.Property(x => x.CostCenterIdSnapshot).HasColumnName("cost_center_id_snapshot");
        builder.Property(x => x.CheckInDate).HasColumnName("check_in_date");
        builder.Property(x => x.CheckOutDateExclusive).HasColumnName("check_out_date_exclusive");
        builder.Property(x => x.NightlyRateSnapshot).HasColumnName("nightly_rate_snapshot").HasPrecision(18, 2);
        builder.Property(x => x.CurrencySnapshot).HasColumnName("currency_snapshot").HasMaxLength(3).IsRequired();
        builder.Property(x => x.TotalCostSnapshot).HasColumnName("total_cost_snapshot").HasPrecision(18, 2);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(2000);
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
        builder.Property(x => x.ClosedBy).HasColumnName("closed_by");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
        CampConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_stays_company");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_stays_employee");
        builder.HasOne<CampSite>().WithMany().HasForeignKey(x => x.CampId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_stays_camp");
        builder.HasOne<CampRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_stays_room");
        builder.HasOne<CampBed>().WithMany().HasForeignKey(x => x.BedId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_stays_bed");
        builder.HasOne<AccommodationRate>().WithMany().HasForeignKey(x => x.RateId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_stays_rate");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_stays_project_snapshot");
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_accommodation_stays_cost_center_snapshot");
        builder.HasIndex(x => new { x.EmployeeId, x.CheckInDate }).HasDatabaseName("ix_accommodation_stays_employee_date");
        builder.HasIndex(x => new { x.CampId, x.Status, x.CheckInDate }).HasDatabaseName("ix_accommodation_stays_camp_status_date");
        builder.HasIndex(x => x.BedId).HasDatabaseName("ix_accommodation_stays_bed");
    }
}
