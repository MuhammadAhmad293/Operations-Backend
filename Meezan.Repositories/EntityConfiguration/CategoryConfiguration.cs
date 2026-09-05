using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Meezan.DataModel.Entities;

namespace Meezan.Repositories.EntityConfiguration
{
    internal class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.Property(e => e.CreationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.LastModificationTime).HasDefaultValueSql("GETDATE()");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.Property(e => e.Name).IsRequired();
            builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
            builder.Property(e => e.SortOrder).HasDefaultValue(0);
            builder.Property(e => e.IsProtected).HasDefaultValue(false);
            builder.Property(e => e.SystemPurpose).HasConversion<string>().HasMaxLength(20);

            builder.HasIndex(e => e.AccountId);

            builder.HasOne(e => e.Account)
                   .WithMany()
                   .HasForeignKey(e => e.AccountId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Parent)
                   .WithMany(e => e.Children)
                   .HasForeignKey(e => e.ParentId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
