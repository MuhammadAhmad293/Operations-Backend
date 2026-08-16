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
            services.AddScoped<IAccountService, AccountService.AccountService>();
            services.AddScoped<IRateService, RateService.RateService>();
            services.AddScoped<ILookupService, LookupService.LookupService>();
            services.AddScoped<IWalletService, WalletService.WalletService>();
            services.AddScoped<ICategoryService, CategoryService.CategoryService>();
            services.AddScoped<IZakatEngine, ZakatEngine.ZakatEngine>();
            services.AddScoped<ITransactionService, TransactionService.TransactionService>();
            services.AddScoped<IAttachmentService, AttachmentService.AttachmentService>();
            services.AddScoped<IOverviewService, OverviewService.OverviewService>();
            services.AddScoped<ICalendarService, CalendarService.CalendarService>();
            services.AddScoped<IStatisticsService, StatisticsService.StatisticsService>();
            services.AddScoped<IZakatPotCalculator, ZakatEngine.ZakatPotCalculator>();
            services.AddScoped<IZakatService, ZakatService.ZakatService>();
            services.AddScoped<INotificationService, NotificationService.NotificationService>();
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
