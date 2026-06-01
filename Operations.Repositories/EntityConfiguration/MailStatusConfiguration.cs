using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operations.DataModel.Entities;

namespace Operations.Repositories.EntityConfiguration
{
    internal class MailStatusConfiguration : IEntityTypeConfiguration<MailStatus>
    {
        public void Configure(EntityTypeBuilder<MailStatus> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
