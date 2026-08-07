using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;
using Meezan.DataModel.Enums;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class MailConfiguration : IEntityTypeConfiguration<Mail>
    {
        public void Configure(EntityTypeBuilder<Mail> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
            builder.Property(e => e.DeliveryStatus).HasDefaultValue(DeliveryStatus.Pending);
            builder.Property(e => e.RetryCount).HasDefaultValue(0);
        }
    }
}
