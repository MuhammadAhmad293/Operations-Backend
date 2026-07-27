using Common.FileHelper;
using Common.HttpClientHelpers;
using Common.Notification.Mail;
using Common.PasswordHash;
using Common.Resolver;
using Common.Validator;
using Operations;
using Operations.Filter;
using Operations.Repositories.Resolver;
using Operations.Services.Email;
using Operations.Services.HealthChecks;
using Operations.Services.Mapper;
using Operations.Services.Messaging;
using Operations.Services.Metrics;
using Operations.Services.Outbox;
using Operations.Services.Resolver;
using Operations.Services.Setting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Hangfire;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Email delivery pipeline
EmailDeliverySettings emailDeliverySettings = new();
builder.Configuration.Bind("EmailDelivery", emailDeliverySettings);
builder.Services.AddSingleton(emailDeliverySettings);

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
    .AddCheck<OutboxBacklogHealthCheck>("outbox-backlog", tags: ["ready"]);

builder.Services.AddControllers();
// add hangfire
builder.Services.AddHangfire(x => x.UseSqlServerStorage(builder.Configuration.GetConnectionString("HFDBConString")));
builder.Services.AddHangfireServer();
//

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();//
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<SwaggerHeaderFilter>();

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

app.UseCors(options => options.WithOrigins("http://localhost:4200").AllowAnyMethod().AllowAnyHeader());

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

app.Run();