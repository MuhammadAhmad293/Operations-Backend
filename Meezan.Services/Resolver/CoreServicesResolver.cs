using Meezan.IServices.IJob;
using Meezan.IServices.IService;
using Meezan.Services.Auth;
using Meezan.Services.Job;
using Meezan.Services.Localization;
using Meezan.Services.Mapper;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Meezan.Services.Resolver
{
    public static class CoreServicesResolver
    {
        public static void ResolveCoreServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IUserService, UserService.UserService>();
            services.AddScoped<IAuthService, AuthService.AuthService>();
            services.AddScoped<ILocalizationService, LocalizationService>();
            services.AddScoped<IJobService, JobService>();
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        }
        public static void ResolveMapper(IServiceCollection services)
        {
            TypeAdapterConfig config = TypeAdapterConfig.GlobalSettings;
            config.Scan(Assembly.GetExecutingAssembly());
            services.AddSingleton(config);
            services.AddScoped<IMapper, ServiceMapper>();
        }
    }
}
