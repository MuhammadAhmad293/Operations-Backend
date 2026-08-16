using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class RateSnapshotConfiguration : IEntityTypeConfiguration<RateSnapshot>
    {
        public void Configure(EntityTypeBuilder<RateSnapshot> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.Property(e => e.FromCurrencyCode).IsRequired().HasMaxLength(10);
            builder.Property(e => e.ToCurrencyCode).IsRequired().HasMaxLength(10);
            builder.Property(e => e.Rate).HasPrecision(18, 6);
            builder.Property(e => e.Source).IsRequired().HasMaxLength(20);

            builder.HasIndex(e => new { e.FromCurrencyCode, e.ToCurrencyCode, e.FetchedAt });

            builder.HasOne(e => e.FromCurrency)
                   .WithMany()
                   .HasForeignKey(e => e.FromCurrencyCode)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.ToCurrency)
                   .WithMany()
                   .HasForeignKey(e => e.ToCurrencyCode)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
