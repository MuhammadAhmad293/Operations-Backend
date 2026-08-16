using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
    {
        public void Configure(EntityTypeBuilder<Currency> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.HasKey(e => e.Code);
            builder.Property(e => e.Code).HasMaxLength(10);
            builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.Symbol).HasMaxLength(10);
        }
    }
}
