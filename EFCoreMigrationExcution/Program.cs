using Meezan.Repositories.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Start Connect To DB To Apply Migration");

using (AppDbContext sc = CreateDbContext())
{
    sc.Database.Migrate();
}

Console.WriteLine("End Migration");
Console.ReadLine();


static AppDbContext CreateDbContext()
{
    IConfigurationRoot configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build();

    var builder = new DbContextOptionsBuilder<AppDbContext>();
    var connectionString = configuration.GetConnectionString("DBConString");
    builder.UseSqlServer(connectionString);
    return new AppDbContext(builder.Options);
}
