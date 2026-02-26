using AiDemo.Application;
using AiDemo.Application.Interfaces;
using AiDemo.Infrastructure;
using AiDemo.Server.Endpoints;
using AiDemo.Server.Hubs;
using AiDemo.Server.Middleware;
using AiDemo.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Debug()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Configure OIDC Authentication (Authentik)
var oidcAuthority = builder.Configuration["Oidc:Authority"] ?? throw new InvalidOperationException("OIDC Authority not configured");
var oidcAudience = builder.Configuration["Oidc:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = oidcAuthority;
        options.RequireHttpsMetadata = false; // Development only - use true in production
        
        if (!string.IsNullOrEmpty(oidcAudience))
        {
            options.Audience = oidcAudience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = oidcAudience,
                ValidateIssuer = true,
                ValidateLifetime = true
            };
        }
        else
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false, // Authentik doesn't always set audience
                ValidateIssuer = true,
                ValidateLifetime = true
            };
        }

        // SignalR specific configuration
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var tokenPreview = context.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(tokenPreview) && tokenPreview.Length > 50)
                {
                    tokenPreview = tokenPreview.Substring(0, 50);
                }
                Console.WriteLine($"Auth failed: {context.Exception.Message}");
                Console.WriteLine($"Token preview: {tokenPreview}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"Token validated for user: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Configure SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Show detailed errors (development only)
})
    .AddMessagePackProtocol();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Rate Limiting (ADR-008)
builder.Services.AddSignalRRateLimiting();

// Add Health Checks
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddNpgSql(connectionString!, name: "database", tags: ["ready", "db"]);

// Register OIDC token service
builder.Services.AddHttpClient<IOidcTokenService, OidcTokenService>();

// Register outbox notification dispatcher
builder.Services.AddScoped<IOutboxNotificationDispatcher, OutboxNotificationDispatcher>();

var app = builder.Build();

// Configure middleware pipeline
app.UseSerilogRequestLogging();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

// Map SignalR hub with connection-level rate limiting
app.MapHub<AppHub>("/hubs/app").RequireRateLimiting(SignalRRateLimitingConfiguration.UserPolicy);

// Map REST endpoints (ADR-004: only health + auth)
app.MapHealthEndpoints();
app.MapAuthEndpoints();

app.Run();
