using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.DateHijri).IsRequired().HasMaxLength(10);
            builder.Property(e => e.Amount).HasPrecision(18, 3);
            builder.Property(e => e.PureGoldGrams).HasPrecision(18, 3);
            builder.Property(e => e.ConvertedAmount).HasPrecision(18, 3);
            builder.Property(e => e.ZakatGoldGrams).HasPrecision(18, 3);
            builder.Property(e => e.ExchangeRate).HasPrecision(18, 6);
            builder.Property(e => e.IsFee).HasDefaultValue(false);

            builder.HasIndex(e => e.AccountId);
            builder.HasIndex(e => new { e.AccountId, e.DateGregorian });
            builder.HasIndex(e => e.ZakatCycleId);

            builder.HasOne(e => e.Account)
                   .WithMany()
                   .HasForeignKey(e => e.AccountId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Wallet)
                   .WithMany()
                   .HasForeignKey(e => e.WalletId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.ToWallet)
                   .WithMany()
                   .HasForeignKey(e => e.ToWalletId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.Category)
                   .WithMany()
                   .HasForeignKey(e => e.CategoryId)
                   .OnDelete(DeleteBehavior.Restrict);

            // BR-09 requires the linked fee transaction to be deleted along with its parent, but
            // SQL Server rejects ON DELETE CASCADE here: combined with Account's cascade into
            // Transaction, a self-referencing cascade creates a "multiple cascade paths" cycle
            // it can't resolve deterministically. The parent-delete-cascades-to-fee guarantee is
            // therefore enforced in TransactionService.Delete (Phase 009), not by the DB.
            builder.HasOne(e => e.ParentTransaction)
                   .WithMany(e => e.FeeTransactions)
                   .HasForeignKey(e => e.ParentTransactionId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
