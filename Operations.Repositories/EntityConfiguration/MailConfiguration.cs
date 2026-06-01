using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operations.DataModel.Entities;

namespace Operations.Repositories.EntityConfiguration
{
    internal class MailConfiguration : IEntityTypeConfiguration<Mail>
    {
        public void Configure(EntityTypeBuilder<Mail> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
