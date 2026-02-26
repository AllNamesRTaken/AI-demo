using AiDemo.Application.Interfaces;
using AiDemo.Infrastructure.Persistence;
using AiDemo.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiDemo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => 
            provider.GetRequiredService<ApplicationDbContext>());

        // Services
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        
        // Background Services
        services.AddHostedService<OutboxProcessorService>();

        return services;
    }
}
