using Operations.DataModel.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Operations.Repositories.Context
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

    }
}
