using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Domain.Identity;
using PersonnelPlatform.Domain.Personnel;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("files", DatabaseSchemas.System);
        builder.HasKey(x => x.Id).HasName("pk_files");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OriginalName).HasColumnName("original_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Extension).HasColumnName("extension").HasMaxLength(20).IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.UploadedBy).HasColumnName("uploaded_by").IsRequired();
        builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at").IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique().HasDatabaseName("ux_files_storage_key");
        builder.HasIndex(x => x.Sha256).HasDatabaseName("ix_files_sha256");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("document_types", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_document_types");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(x => x.RequiredByDefault).HasColumnName("required_by_default").IsRequired();
        builder.Property(x => x.ExpirationRequired).HasColumnName("expiration_required").IsRequired();
        builder.Property(x => x.DefaultValidityDays).HasColumnName("default_validity_days");
        builder.Property(x => x.FileRequired).HasColumnName("file_required").IsRequired();
        builder.Property(x => x.DocumentNumberRequired).HasColumnName("document_number_required").IsRequired();
        builder.Property(x => x.MultipleAllowed).HasColumnName("multiple_allowed").IsRequired();
        builder.Property(x => x.ReminderDaysCsv).HasColumnName("reminder_days_csv").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_document_types_code");
    }
}

public sealed class DocumentTypeEmployeeTypeRequirementConfiguration : IEntityTypeConfiguration<DocumentTypeEmployeeTypeRequirement>
{
    public void Configure(EntityTypeBuilder<DocumentTypeEmployeeTypeRequirement> builder)
    {
        builder.ToTable("document_type_employee_types", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_document_type_employee_types");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DocumentTypeId).HasColumnName("document_type_id").IsRequired();
        builder.Property(x => x.EmployeeTypeId).HasColumnName("employee_type_id").IsRequired();
        builder.Property(x => x.IsRequired).HasColumnName("is_required").IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.HasIndex(x => new { x.DocumentTypeId, x.EmployeeTypeId }).IsUnique().HasDatabaseName("ux_document_type_employee_types_type_employee");
        builder.HasOne<DocumentType>().WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EmployeeType>().WithMany().HasForeignKey(x => x.EmployeeTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("employee_documents", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_employee_documents");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.Property(x => x.DocumentTypeId).HasColumnName("document_type_id").IsRequired();
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.DocumentNumber).HasColumnName("document_number").HasMaxLength(150);
        builder.Property(x => x.IssueDate).HasColumnName("issue_date");
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from");
        builder.Property(x => x.ValidUntil).HasColumnName("valid_until");
        builder.Property(x => x.IssuingAuthority).HasColumnName("issuing_authority").HasMaxLength(200);
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(3);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(x => x.ReplacesDocumentId).HasColumnName("replaces_document_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.EmployeeId, x.DocumentTypeId }).HasDatabaseName("ix_employee_documents_employee_type");
        builder.HasIndex(x => new { x.Status, x.ValidUntil }).HasDatabaseName("ix_employee_documents_status_valid_until");
        builder.HasIndex(x => x.FileId).HasDatabaseName("ix_employee_documents_file_id");
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentType>().WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.ReplacesDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
