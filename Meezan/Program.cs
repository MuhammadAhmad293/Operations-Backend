using Common.FileHelper;
using Common.FileStorage;
using Common.HttpClientHelpers;
using Common.Notification.Mail;
using Common.PasswordHash;
using Common.Resolver;
using Common.Validator;
using Meezan;
using Meezan.Filter;
using Meezan.Repositories.Resolver;
using Meezan.IServices.IJob;
using Meezan.Jobs;
using Meezan.IServices.IService;
using Meezan.Services.Email;
using Meezan.Services.HealthChecks;
using Meezan.Services.Mapper;
using Meezan.Services.Messaging;
using Meezan.Services.Metrics;
using Meezan.Services.Outbox;
using Meezan.Services.RateProviders;
using Meezan.Services.Resolver;
using Meezan.Services.Setting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Hangfire;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using System.Text;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPasswordHash, PasswordHash>();

CoreServicesResolver.ResolveCoreServices(builder.Services, builder.Configuration);
CoreServicesResolver.ResolveMapper(builder.Services);
CommonResolver.ResolveCommonServices(builder.Services, builder.Configuration);
UnitOfWorkResolver.ResolveUintOfWork(builder.Services, builder.Configuration);
UnitOfWorkResolver.ResolveLazier(builder.Services, builder.Configuration);

// Bind Unit Setting
MailSettings MailSetting = new();
builder.Configuration.Bind("MailSetting", MailSetting);
builder.Services.AddSingleton(MailSetting);

MailSetting emailSetting = new();
builder.Configuration.Bind("MailSetting", emailSetting);
builder.Services.AddSingleton(emailSetting);

builder.Services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddTransient(s =>
{
    IHttpContextAccessor contextAccessor = s.GetService<IHttpContextAccessor>();
    ClaimsPrincipal user = contextAccessor?.HttpContext?.User;
    return user;
});

// Bind JWT settings (secret sourced from user-secrets / env var — not appsettings.json)
JwtSettings jwtSettings = new();
builder.Configuration.Bind("JwtSettings", jwtSettings);
if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
    throw new InvalidOperationException(
        "JwtSettings:Secret is not configured. " +
        "Set it via 'dotnet user-secrets' (dev) or the JwtSettings__Secret environment variable (prod).");
builder.Services.AddSingleton(jwtSettings);

RefreshTokenSettings refreshTokenSettings = new();
builder.Configuration.Bind("RefreshTokenSettings", refreshTokenSettings);
builder.Services.AddSingleton(refreshTokenSettings);

FileStorageSettings fileStorageSettings = new();
builder.Configuration.Bind("FileStorageSettings", fileStorageSettings);
builder.Services.AddSingleton(fileStorageSettings);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddRateLimiter(options =>
{
    // 5 req/min per IP — register, forgot-password, reset-password
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0
            }));

    // 10 req/min per IP — login
    options.AddPolicy("auth-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                QueueLimit = 0
            }));

    // 30 req/min per IP — refresh-token is called automatically/frequently by legitimate clients
    options.AddPolicy("auth-refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 30,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Email delivery pipeline
EmailDeliverySettings emailDeliverySettings = new();
builder.Configuration.Bind("EmailDelivery", emailDeliverySettings);
builder.Services.AddSingleton(emailDeliverySettings);

// Rate integration (Frankfurter/GoldApi/Redis) — BR-19
RateIntegrationSettings rateIntegrationSettings = new();
builder.Configuration.Bind("RateIntegration", rateIntegrationSettings);
builder.Services.AddSingleton(rateIntegrationSettings);
builder.Services.AddHttpClient<FrankfurterRateProvider>();
builder.Services.AddHttpClient<GoldApiRateProvider>();
builder.Services.AddScoped<IRateProvider, CompositeRateProvider>();

// Redis: rate-cache read path (BR-19: Redis → DB fallback, never blocks startup —
// AbortOnConnectFail=false lets the multiplexer come up even if Redis isn't reachable yet
// and keep retrying in the background, matching this repo's RabbitMQ resilience pattern)
ConfigurationOptions redisConfigurationOptions = ConfigurationOptions.Parse(rateIntegrationSettings.Redis.ConnectionString);
redisConfigurationOptions.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConfigurationOptions));
builder.Services.AddStackExchangeRedisCache(options => options.Configuration = rateIntegrationSettings.Redis.ConnectionString);

builder.Services.AddSingleton<RabbitConnectionManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitConnectionManager>());

builder.Services.AddSingleton<IRabbitPublisher, RabbitPublisher>();
builder.Services.AddSingleton<EmailResiliencePipeline>();
builder.Services.AddScoped<ISmtpEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<EmailMetrics>();

builder.Services.AddHostedService<OutboxPublisherService>();
builder.Services.AddHostedService<EmailConsumer>();
builder.Services.AddHostedService<DeadLetterHandler>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"])
    .AddCheck<OutboxBacklogHealthCheck>("outbox-backlog", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

builder.Services.AddControllers();
// add hangfire
builder.Services.AddHangfire(x => x.UseSqlServerStorage(builder.Configuration.GetConnectionString("HFDBConString")));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<IJobEnqueuer, HangfireJobEnqueuer>();
//

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();//
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<SwaggerHeaderFilter>();
    c.SchemaFilter<EnumStringSchemaFilter>();

    foreach (string xmlFile in new[] { "Meezan.xml", "Meezan.Dto.xml" })
    {
        string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
    }

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT token you received from Login/Register. Just the raw token — no need to type \"Bearer \" in front of it."
    });

    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});//



//builder.Services.AddMapster();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())//
{
    app.UseSwagger();//
    //app.UseSwaggerUI();//
    app.UseSwaggerUI(c =>
    {
        string swaggerJsonBasePath = string.IsNullOrWhiteSpace(c.RoutePrefix) ? "." : "..";
        // this line to run swagger when publish api to IIS 
        string SwaggerUrl = builder.Configuration.GetValue<string>("SwaggerUrl");
        // Replace {...} with microservice name
        c.SwaggerEndpoint($"{SwaggerUrl}/swagger/v1/swagger.json", "Opertions Api V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors(options => options
    .WithOrigins("https://localhost:4200")
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());

app.UseHttpsRedirection();//

app.UseMiddleware(typeof(ErrorHandlingMiddleware));

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

#region Localization
List<CultureInfo> cultures = new()
{
    new CultureInfo("en"),
    new CultureInfo("ar")
};
app.UseRequestLocalization(option =>
{
    option.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en");
    option.SupportedCultures = cultures;
    option.SupportedUICultures = cultures;
});
#endregion

app.MapControllers();

app.MapHealthChecks("/health");

app.UseHangfireDashboard();

using (IServiceScope startupScope = app.Services.CreateScope())
{
    IRecurringJobManager recurringJobManager = startupScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<IJobService>(
        "cleanup-refresh-tokens",
        js => js.CleanupExpiredRefreshTokens(),
        Cron.Daily);

    // BR-19: daily rate sync, plus one immediate fire-and-forget run so a fresh install (empty
    // RateSnapshot table) has usable rates within minutes instead of waiting for the first cron tick.
    recurringJobManager.AddOrUpdate<IJobService>(
        "rate-sync",
        js => js.SyncRates(),
        rateIntegrationSettings.CronExpression);
    BackgroundJob.Enqueue<IJobService>(js => js.SyncRates());
}

app.Run();