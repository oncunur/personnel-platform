using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonnelPlatform.Domain.Documents;
using PersonnelPlatform.Domain.Leave;

namespace PersonnelPlatform.Infrastructure.Persistence.Configurations;

public sealed class LeaveAttachmentConfiguration : IEntityTypeConfiguration<LeaveAttachment>
{
    public void Configure(EntityTypeBuilder<LeaveAttachment> builder)
    {
        builder.ToTable("leave_attachments", DatabaseSchemas.Hr);
        builder.HasKey(x => x.Id).HasName("pk_leave_attachments");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeaveId).HasColumnName("leave_id");
        builder.Property(x => x.FileId).HasColumnName("file_id");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        LeaveTypeConfiguration.ConfigureAudit(builder);
        builder.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_leave_attachments_leave");
        builder.HasOne<StoredFile>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_leave_attachments_file");
        builder.HasIndex(x => x.LeaveId).HasDatabaseName("ix_leave_attachments_leave");
        builder.HasIndex(x => x.FileId).IsUnique().HasDatabaseName("ux_leave_attachments_file");
    }
}
