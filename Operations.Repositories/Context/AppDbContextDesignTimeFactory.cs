using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Operations.Repositories.Context
{
    public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            string cwd = Directory.GetCurrentDirectory();
            string appSettingsDir = File.Exists(Path.Combine(cwd, "Operations", "appsettings.json"))
                ? Path.Combine(cwd, "Operations")
                : Path.Combine(cwd, "..", "Operations");

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(appSettingsDir)
                .AddJsonFile("appsettings.json")
                .Build();

            DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
            optionsBuilder.UseSqlServer(configuration.GetConnectionString("DBConString"));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
