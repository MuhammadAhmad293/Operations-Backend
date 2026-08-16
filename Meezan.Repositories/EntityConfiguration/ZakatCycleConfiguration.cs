using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class ZakatCycleConfiguration : IEntityTypeConfiguration<ZakatCycle>
    {
        public void Configure(EntityTypeBuilder<ZakatCycle> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.Property(e => e.HawlStartHijri).IsRequired().HasMaxLength(10);
            builder.Property(e => e.HawlDueHijri).IsRequired().HasMaxLength(10);
            builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.PotGoldGramsAtDue).HasPrecision(18, 3);
            builder.Property(e => e.ZakatDueGoldGrams).HasPrecision(18, 3);
            builder.Property(e => e.ExternalPaidGoldGrams).HasPrecision(18, 3);

            builder.HasIndex(e => e.AccountId);
            builder.HasIndex(e => new { e.AccountId, e.Status });

            builder.HasOne(e => e.Account)
                   .WithMany()
                   .HasForeignKey(e => e.AccountId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Completes the Transaction.ZakatCycleId FK deferred in TransactionConfiguration
            // (sub-task 6) — ZakatCycle didn't exist yet at that point.
            builder.HasMany(e => e.Payments)
                   .WithOne()
                   .HasForeignKey(t => t.ZakatCycleId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
