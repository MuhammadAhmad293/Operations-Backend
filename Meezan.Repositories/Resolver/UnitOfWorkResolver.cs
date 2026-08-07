using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Meezan.IRepositories.UnitOfWork;

namespace Meezan.Repositories.Resolver
{
    public static class UnitOfWorkResolver
    {
        public static void ResolveUintOfWork(IServiceCollection services, IConfiguration configuration)
        {            
            // Add ConString To Db Context
            services.AddDbContext<Context.AppDbContext>(cnf =>
            {
                cnf.UseSqlServer(configuration.GetConnectionString("DBConString"));
                cnf.UseLazyLoadingProxies(false);
            });

            // Resolve UnitOfWork
            services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

            services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<Context.AppDbContext>()
                .AddDefaultTokenProviders();
        }
        public static void ResolveLazier(IServiceCollection services, IConfiguration configuration)
        {
            // Resolve Lazier
            services.AddScoped(typeof(Lazy<>), typeof(Lazier<>));
        }
    }
}
