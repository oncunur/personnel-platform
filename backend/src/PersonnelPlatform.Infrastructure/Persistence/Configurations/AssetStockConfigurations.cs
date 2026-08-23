using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Administration;
using PersonnelPlatform.Domain.Common;
using PersonnelPlatform.Domain.Organization;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

internal static class AdministrationConfigurationHelpers
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

public sealed class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    public void Configure(EntityTypeBuilder<StockLocation> builder)
    {
        builder.ToTable("stock_locations", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_stock_locations");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_stock_locations_company");
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_stock_locations_company_code");
    }
}

public sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_stock_items");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(20);
        builder.Property(x => x.MinimumLevel).HasColumnName("minimum_level").HasPrecision(18, 3);
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_stock_items_company");
        builder.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique().HasDatabaseName("ux_stock_items_company_code");
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_stock_movements");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.StockItemId).HasColumnName("stock_item_id");
        builder.Property(x => x.LocationId).HasColumnName("location_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.ProjectIdSnapshot).HasColumnName("project_id_snapshot");
        builder.Property(x => x.CostCenterIdSnapshot).HasColumnName("cost_center_id_snapshot");
        builder.Property(x => x.MovementType).HasColumnName("movement_type").HasMaxLength(30);
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 3);
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(20);
        builder.Property(x => x.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(200);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Ignore(x => x.SignedQuantity);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<StockItem>().WithMany().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_stock_movements_item");
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_stock_movements_location");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_stock_movements_employee");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_stock_movements_project_snapshot");
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_stock_movements_cost_center_snapshot");
        builder.HasIndex(x => new { x.StockItemId, x.LocationId, x.OccurredAt }).HasDatabaseName("ix_stock_movements_item_location_time");
        builder.HasIndex(x => new { x.CompanyId, x.Source, x.ExternalEventId }).HasDatabaseName("ix_stock_movements_external_model");
    }
}

public sealed class AssetItemConfiguration : IEntityTypeConfiguration<AssetItem>
{
    public void Configure(EntityTypeBuilder<AssetItem> builder)
    {
        builder.ToTable("assets", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_assets");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.LocationId).HasColumnName("location_id");
        builder.Property(x => x.AssetTag).HasColumnName("asset_tag").HasMaxLength(80);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(100);
        builder.Property(x => x.SerialNumber).HasColumnName("serial_number").HasMaxLength(150);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.PurchaseDate).HasColumnName("purchase_date");
        builder.Property(x => x.PurchaseCost).HasColumnName("purchase_cost").HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_assets_company");
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_assets_location");
        builder.HasIndex(x => new { x.CompanyId, x.AssetTag }).IsUnique().HasDatabaseName("ux_assets_company_tag");
        builder.HasIndex(x => new { x.CompanyId, x.SerialNumber }).HasDatabaseName("ix_assets_company_serial_model");
    }
}

public sealed class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        builder.ToTable("asset_assignments", DatabaseSchemas.Administration);
        builder.HasKey(x => x.Id).HasName("pk_asset_assignments");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CompanyId).HasColumnName("company_id");
        builder.Property(x => x.AssetId).HasColumnName("asset_id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id");
        builder.Property(x => x.ProjectIdSnapshot).HasColumnName("project_id_snapshot");
        builder.Property(x => x.CostCenterIdSnapshot).HasColumnName("cost_center_id_snapshot");
        builder.Property(x => x.AssignedDate).HasColumnName("assigned_date");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.ReturnedDate).HasColumnName("returned_date");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(1000);
        AdministrationConfigurationHelpers.ConfigureAudit(builder);
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_asset_assignments_company");
        builder.HasOne<AssetItem>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_asset_assignments_asset");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_asset_assignments_employee");
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_asset_assignments_project_snapshot");
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.CostCenterIdSnapshot).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_asset_assignments_cost_center_snapshot");
        builder.HasIndex(x => new { x.AssetId, x.Status }).HasDatabaseName("ix_asset_assignments_asset_status");
        builder.HasIndex(x => new { x.EmployeeId, x.AssignedDate }).HasDatabaseName("ix_asset_assignments_employee_date");
    }
}
