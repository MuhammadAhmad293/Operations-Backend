using Common.FileHelper;
using Common.HttpClientHelpers;
using Common.Notification.Mail;
using Common.Validator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Common.Resolver
{
    public static class CommonResolver
    {
        public static void ResolveCommonServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IMailSender, MailSender>();
            services.AddScoped<IFileHelper, FileHelper.FileHelper>();
            services.AddScoped<IValidatorHelper, ValidatorHelper>();
            services.AddScoped<IHttpClientHelper, HttpClientHelper>();
        }
    }
}
