using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Operations.DataModel.Entities;

namespace Operations.Repositories.EntityConfiguration
{
    internal class MailAttachmentConfiguration : IEntityTypeConfiguration<MailAttachment>
    {
        public void Configure(EntityTypeBuilder<MailAttachment> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
