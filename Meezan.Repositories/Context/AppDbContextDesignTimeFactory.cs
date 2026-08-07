using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Meezan.Repositories.Context
{
    public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            string cwd = Directory.GetCurrentDirectory();
            string appSettingsDir = File.Exists(Path.Combine(cwd, "Meezan", "appsettings.json"))
                ? Path.Combine(cwd, "Meezan")
                : Path.Combine(cwd, "..", "Meezan");

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
