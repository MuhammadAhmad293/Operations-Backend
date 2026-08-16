using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class WalletConfiguration : IEntityTypeConfiguration<Wallet>
    {
        public void Configure(EntityTypeBuilder<Wallet> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.Property(e => e.Name).IsRequired();
            builder.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(10);
            builder.Property(e => e.InitialAmount).HasPrecision(18, 3).HasDefaultValue(0m);
            builder.Property(e => e.ExcludeFromTotal).HasDefaultValue(false);
            builder.Property(e => e.IsArchived).HasDefaultValue(false);

            builder.HasIndex(e => e.AccountId);

            builder.HasOne(e => e.Account)
                   .WithMany()
                   .HasForeignKey(e => e.AccountId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.WalletType)
                   .WithMany()
                   .HasForeignKey(e => e.WalletTypeId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.Currency)
                   .WithMany()
                   .HasForeignKey(e => e.CurrencyCode)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
