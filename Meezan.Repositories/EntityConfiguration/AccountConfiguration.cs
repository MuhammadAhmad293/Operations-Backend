using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.Property(e => e.Name).IsRequired();
            builder.Property(e => e.BaseCurrencyCode).IsRequired().HasMaxLength(10);
            builder.Property(e => e.DisplayCalendar).HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.Theme).HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.Language).HasConversion<string>().HasMaxLength(20);

            builder.HasIndex(e => e.UserId).IsUnique();

            builder.HasOne(e => e.User)
                   .WithMany()
                   .HasForeignKey(e => e.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.BaseCurrency)
                   .WithMany()
                   .HasForeignKey(e => e.BaseCurrencyCode)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
