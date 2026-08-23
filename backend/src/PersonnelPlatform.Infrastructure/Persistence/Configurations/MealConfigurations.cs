using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Camp;
using PersonnelPlatform.Domain.Common;
using PersonnelPlatform.Domain.Meal;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class MealConfigurationHelpers
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

public sealed class MealTypeConfiguration : IEntityTypeConfiguration<MealType>
{
    public void Configure(EntityTypeBuilder<MealType> builder)
    {
        builder.ToTable("meal_types", DatabaseSchemas.Meal);
        builder.HasKey(x => x.Id).HasName("pk_meal_types");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_meal_types_code");
    }
}

public sealed class MealRateConfiguration : IEntityTypeConfiguration<MealRate>
{
    public void Configure(EntityTypeBuilder<MealRate> builder)
    {
        builder.ToTable("meal_rates", DatabaseSchemas.Meal);
        builder.HasKey(x => x.Id).HasName("pk_meal_rates");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CampId).HasColumnName("camp_id");
        builder.Property(x => x.MealTypeId).HasColumnName("meal_type_id");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntilExclusive).HasColumnName("valid_until_exclusive");
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        MealConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<CampSite>().WithMany().HasForeignKey(x => x.CampId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_rates_camp");
        builder.HasOne<MealType>().WithMany().HasForeignKey(x => x.MealTypeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_rates_type");
        builder.HasIndex(x => new { x.CampId, x.MealTypeId, x.ValidFrom }).HasDatabaseName("ix_meal_rates_camp_type_from");
    }
}

public sealed class MealConsumptionConfiguration : IEntityTypeConfiguration<MealConsumption>
{
    public void Configure(EntityTypeBuilder<MealConsumption> builder)
    {
        builder.ToTable("meal_consumptions", DatabaseSchemas.Meal);
        builder.HasKey(x => x.Id).HasName("pk_meal_consumptions");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.CampId).HasColumnName("camp_id");
        builder.Property(x => x.MealTypeId).HasColumnName("meal_type_id");
        builder.Property(x => x.MealRateId).HasColumnName("meal_rate_id");
        builder.Property(x => x.ConsumptionDate).HasColumnName("consumption_date");
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(10, 2);
        builder.Property(x => x.UnitPriceSnapshot).HasColumnName("unit_price_snapshot").HasPrecision(18, 2);
        builder.Property(x => x.CurrencySnapshot).HasColumnName("currency_snapshot").HasMaxLength(3).IsRequired();
        builder.Property(x => x.TotalCostSnapshot).HasColumnName("total_cost_snapshot").HasPrecision(18, 2);
        builder.Property(x => x.ProjectIdSnapshot).HasColumnName("project_id_snapshot");
        builder.Property(x => x.CostCenterIdSnapshot).HasColumnName("cost_center_id_snapshot");
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(200);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        MealConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_consumptions_company");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_consumptions_employee");
        builder.HasOne<CampSite>().WithMany().HasForeignKey(x => x.CampId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_consumptions_camp");
        builder.HasOne<MealType>().WithMany().HasForeignKey(x => x.MealTypeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_consumptions_type");
        builder.HasOne<MealRate>().WithMany().HasForeignKey(x => x.MealRateId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_consumptions_rate");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_consumptions_project_snapshot");
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_meal_consumptions_cost_center_snapshot");
        builder.HasIndex(x => new { x.EmployeeId, x.ConsumptionDate, x.MealTypeId }).HasDatabaseName("ix_meal_consumptions_employee_date_type_model");
        builder.HasIndex(x => new { x.CampId, x.ConsumptionDate }).HasDatabaseName("ix_meal_consumptions_camp_date");
    }
}
