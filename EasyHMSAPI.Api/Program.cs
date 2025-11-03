using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.DependencyCollector;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// 1️⃣ Load configuration
// ------------------------------------------------------------
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ------------------------------------------------------------
// 2️⃣ Logging setup (console + Azure + App Insights)
// ------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddAzureWebAppDiagnostics();

// ✅ Add Application Insights Logging & Telemetry
// Pulls Connection String from appsettings.json or environment variables
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration["ApplicationInsights:ConnectionString"]);

// (Optional) — configure adaptive sampling and dependency tracking
builder.Services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) =>
{
    module.EnableSqlCommandTextInstrumentation = true; // capture SQL queries
});
builder.Services.Configure<TelemetryConfiguration>((config) =>
{
    // Optional: Enable/disable adaptive sampling (default is enabled)
    // config.DefaultTelemetrySink.TelemetryProcessorChainBuilder.UseSampling(5.0);
});

// ------------------------------------------------------------
// 3️⃣ Add services
// ------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

// ------------------------------------------------------------
// 4️⃣ Swagger
// ------------------------------------------------------------
// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "NexEagle EasyHMS API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ------------------------------------------------------------
// 5️⃣ CORS
// ------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ------------------------------------------------------------
// 6️⃣ JWT Auth
// ------------------------------------------------------------
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("Jwt:Issuer missing.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("Jwt:Audience missing.");
var jwtSecret = builder.Configuration["Jwt:SecretKey"]
                ?? throw new InvalidOperationException("Jwt:SecretKey missing.");

if (jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:SecretKey too short; use at least 32 chars.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

// ------------------------------------------------------------
// 7️⃣ EF Core
// ------------------------------------------------------------
var sqlConn = builder.Configuration.GetConnectionString("DefaultConnection")
             ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        sqlConn,
        sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            sql.CommandTimeout(60);
        }
    ));

// ------------------------------------------------------------
// 8️⃣ MediatR
// ------------------------------------------------------------
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UserLoginHandler).Assembly));

// ------------------------------------------------------------
// 9️⃣ Custom Services
// ------------------------------------------------------------
builder.Services.AddScoped<IJwtAuthService, JwtAuthService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ------------------------------------------------------------
// 🔟 Azure App Service File Logger Options (optional)
// ------------------------------------------------------------
builder.Services.Configure<AzureFileLoggerOptions>(options =>
{
    options.FileName = "apnahospital-log-";
    options.RetainedFileCountLimit = 5;
});

// ------------------------------------------------------------
// 11️⃣ Build and Configure Pipeline
// ------------------------------------------------------------
// --- App Pipeline ---
var app = builder.Build();

var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled")
 || !app.Environment.IsProduction();

if (swaggerEnabled)
{
 app.UseSwagger();
 app.UseSwaggerUI(c =>
 {
 c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexEagle EasyHMS API v1");
 });
}

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("FrontendCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Simple redirect for root
app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger/index.html");
    return Task.CompletedTask;
});

app.Run();
