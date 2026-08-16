using Meezan.DataModel.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Meezan.Repositories.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            SingularizeTableNames(modelBuilder);
            ModelConfiguration(modelBuilder);
            base.OnModelCreating(modelBuilder);
            modelBuilder.SeedInitialData();
        }
        private static void SingularizeTableNames(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                modelBuilder.Entity(entityType.ClrType).ToTable(entityType.ClrType.Name);
        }
        private static void ModelConfiguration(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        public DbSet<User> User { get; set; }
        public DbSet<Mail> Mail { get; set; }
        public DbSet<PasswordResetToken> PasswordResetToken { get; set; }
        public DbSet<OutboxMessage> OutboxMessage { get; set; }
        public DbSet<ProcessedMessage> ProcessedMessage { get; set; }
        public DbSet<RefreshToken> RefreshToken { get; set; }

        public DbSet<Account> Account { get; set; }
        public DbSet<Currency> Currency { get; set; }
        public DbSet<WalletType> WalletType { get; set; }
        public DbSet<Wallet> Wallet { get; set; }
        public DbSet<Category> Category { get; set; }
        public DbSet<Transaction> Transaction { get; set; }
        public DbSet<Attachment> Attachment { get; set; }
        public DbSet<RateSnapshot> RateSnapshot { get; set; }
        public DbSet<ZakatCycle> ZakatCycle { get; set; }

    }
}
