using Operations.IServices.IJob;
using Operations.IServices.IService;
using Operations.Services.Auth;
using Operations.Services.Job;
using Operations.Services.Localization;
using Operations.Services.Mapper;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Operations.Services.Resolver
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
