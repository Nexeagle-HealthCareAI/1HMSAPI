using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Helpers.Implementations;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// Load configuration
// ------------------------------------------------------------
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    // Untracked local overrides for real secrets (see .gitignore). Loaded after the committed
    // env file so it wins over the placeholder values, but before env vars so prod still overrides.
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ------------------------------------------------------------
// Logging setup (console + debug; container stdout is collected on the VM)
// ------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ------------------------------------------------------------
// Add services
// ------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
// Multi-tenant guard: blocks a signed-in user from acting on a hospital they don't belong to.
builder.Services.AddScoped<EasyHMSAPI.Api.Common.HospitalAccessFilter>();
// Public (Nexeagle) API-key gate — applied per-controller via [ServiceFilter], not globally.
builder.Services.AddScoped<EasyHMSAPI.Api.Common.PublicApiKeyFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<EasyHMSAPI.Api.Common.HospitalAccessFilter>();
});

// ------------------------------------------------------------
// Swagger
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
// CORS
// ------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "https://1hms.nexeagle.com",
                "http://1hms.nexeagle.com",
                "https://1hms-dev.nexeagle.com",
                "https://nexeagle.com",
                "http://nexeagle.com",
                "http://151.185.45.77:81",
                "http://151.185.45.67:81"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Explicit origins allow for credentials if needed
    });
});

// ------------------------------------------------------------
// JWT Auth
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
            // Zero tolerance here means any clock drift between the process that issued a token
            // and the process validating it (e.g. a container clock a few seconds off host time)
            // rejects an otherwise-valid, non-expired token outright. A small buffer is the
            // standard mitigation — tokens still expire on schedule (see JwtAuthService, 1 hour),
            // this only forgives a few seconds/minutes of clock disagreement at the edges.
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

// ------------------------------------------------------------
// EF Core
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
// MediatR
// ------------------------------------------------------------
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UserLoginHandler).Assembly));

// ------------------------------------------------------------
// Custom Services
// ------------------------------------------------------------
builder.Services.AddScoped<IJwtAuthService, JwtAuthService>();
builder.Services.AddScoped<IMaskingService, MaskingService>();
// Object storage: S3-compatible (MinIO) bucket.
builder.Services.AddScoped<IBlobStorageService, S3StorageService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IWhatsAppMessagingService, WhatsAppMessagingService>();
builder.Services.AddScoped<EasyHMSAPI.Application.Services.Interfaces.IPatientTokenValidator, EasyHMSAPI.Application.Services.Implementations.PatientTokenValidator>();
builder.Services.AddScoped<EasyHMSAPI.Application.Services.Interfaces.IGeoIpLookupService, EasyHMSAPI.Application.Services.Implementations.IpApiGeoLookupService>();
builder.Services.AddScoped<IVoiceRxService, VoiceRxService>();
builder.Services.AddScoped<IDoctorValidationHelper, DoctorValidationHelper>();
builder.Services.AddScoped<ISubscriptionLimitHelper, SubscriptionLimitHelper>();
// ABDM M1: ABHA creation (Aadhaar-OTP) + existing-ABHA login (Mobile/Aadhaar-OTP).
builder.Services.AddScoped<EasyHMSAPI.Application.Services.Interfaces.IAbdmEncryptionService, EasyHMSAPI.Application.Services.Implementations.AbdmEncryptionService>();
builder.Services.AddScoped<EasyHMSAPI.Application.Services.Interfaces.IAbdmGatewayService, EasyHMSAPI.Application.Services.Implementations.AbdmGatewayService>();
builder.Services.AddScoped<EasyHMSAPI.Application.Services.Interfaces.IAbdmAbhaService, EasyHMSAPI.Application.Services.Implementations.AbdmAbhaService>();

// RxNorm (RxNav): free, unauthenticated NLM API used to enrich a medicine's generic/salt
// ingredients (available strengths, US-naming cross-reference). Fixed public base URL, no
// per-environment config needed.
builder.Services.AddHttpClient<EasyHMSAPI.Application.Services.Interfaces.IRxNormService, EasyHMSAPI.Application.Services.Implementations.RxNormService>(client =>
{
    client.BaseAddress = new Uri("https://rxnav.nlm.nih.gov/REST/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ------------------------------------------------------------
// Rate Limiting
// ------------------------------------------------------------
// NexEagleWebsite proxies every public/patient-auth call server-to-server (see easyhmsFetch) —
// both apps run in separate Docker containers on the same VM, so RemoteIpAddress alone sees the
// Docker bridge address for ALL of that traffic, not the actual visitor's IP. TrustedProxyIpResolver
// recovers the real IP from a header, but only trusts it when a shared secret also matches —
// unset by default (see appsettings.json), in which case every policy below behaves exactly as
// before (falls back to RemoteIpAddress).
var proxyForwardingSecret = builder.Configuration["Internal:ProxyForwardingSecret"];

builder.Services.AddRateLimiter(options =>
{
     options.AddPolicy("PerIpPolicy", context =>
         RateLimitPartition.GetFixedWindowLimiter(
         partitionKey: EasyHMSAPI.Api.Common.TrustedProxyIpResolver.Resolve(context, proxyForwardingSecret),
         factory: key => new FixedWindowRateLimiterOptions
         {
             PermitLimit = 100,
             Window = TimeSpan.FromMinutes(1),
             QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
             QueueLimit = 0
         })
     );

     // Public (Nexeagle) endpoints — external, unauthenticated-by-JWT surface, tighter than
     // the general per-IP policy above since a leaked/scraped API key is a higher abuse risk.
     options.AddPolicy("PublicBookingPolicy", context =>
         RateLimitPartition.GetFixedWindowLimiter(
         partitionKey: EasyHMSAPI.Api.Common.TrustedProxyIpResolver.Resolve(context, proxyForwardingSecret),
         factory: key => new FixedWindowRateLimiterOptions
         {
             PermitLimit = 20,
             Window = TimeSpan.FromMinutes(1),
             QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
             QueueLimit = 0
         })
     );

     // Patient WhatsApp-OTP login (Doctor Dekho) — tighter per-IP ceiling than PublicBookingPolicy.
     // This is on top of, not instead of, the per-mobile-number cooldown/daily-cap enforced inside
     // PatientOtpSendHandler itself: this policy stops one IP from hammering many different numbers,
     // the handler-level check stops any single number from being spammed regardless of IP rotation.
     options.AddPolicy("PatientAuthPolicy", context =>
         RateLimitPartition.GetFixedWindowLimiter(
         partitionKey: EasyHMSAPI.Api.Common.TrustedProxyIpResolver.Resolve(context, proxyForwardingSecret),
         factory: key => new FixedWindowRateLimiterOptions
         {
             PermitLimit = 8,
             Window = TimeSpan.FromMinutes(1),
             QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
             QueueLimit = 0
         })
     );

     // Page-view beacons — fires on every page load, so this needs a much more generous ceiling
     // than the booking/auth policies above (a visitor browsing normally shouldn't get throttled).
     options.AddPolicy("TrackVisitPolicy", context =>
         RateLimitPartition.GetFixedWindowLimiter(
         partitionKey: EasyHMSAPI.Api.Common.TrustedProxyIpResolver.Resolve(context, proxyForwardingSecret),
         factory: key => new FixedWindowRateLimiterOptions
         {
             PermitLimit = 60,
             Window = TimeSpan.FromMinutes(1),
             QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
             QueueLimit = 0
         })
     );
});

// ------------------------------------------------------------
// Build and Configure Pipeline
// ------------------------------------------------------------
// --- App Pipeline ---
var app = builder.Build();

// Always enable Swagger, regardless of environment
app.UseSwagger();
app.UseSwaggerUI(c =>
{
 c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexEagle EasyHMS API v1");
});

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("FrontendCors");
app.UseRateLimiter();
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
