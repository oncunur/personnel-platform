using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Domain.Identity;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class EmployeeDocumentHistoryConfiguration : IEntityTypeConfiguration<EmployeeDocumentHistory>
{
    public void Configure(EntityTypeBuilder<EmployeeDocumentHistory> builder)
    {
        builder.ToTable("employee_document_history", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_employee_document_history");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EmployeeDocumentId).HasColumnName("employee_document_id").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(40).IsRequired();
        builder.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(30);
        builder.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by").IsRequired();
        builder.Property(x => x.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        builder.HasIndex(x => new { x.EmployeeDocumentId, x.ChangedAt }).HasDatabaseName("ix_employee_document_history_document_changed_at");
        builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.EmployeeDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
