using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
            builder.Property(e => e.Status).HasDefaultValue(OutboxStatus.Pending);
            builder.Property(e => e.RetryCount).HasDefaultValue(0);

            builder.HasIndex(e => new { e.Status, e.CreationTime });

            builder.HasOne(e => e.Mail)
                   .WithMany()
                   .HasForeignKey(e => e.MailId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}