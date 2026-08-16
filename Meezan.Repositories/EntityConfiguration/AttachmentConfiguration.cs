using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.Property(e => e.FileName).IsRequired();
            builder.Property(e => e.MimeType).IsRequired().HasMaxLength(100);
            builder.Property(e => e.StoragePath).IsRequired();

            builder.HasIndex(e => e.TransactionId);

            builder.HasOne(e => e.Transaction)
                   .WithMany()
                   .HasForeignKey(e => e.TransactionId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
