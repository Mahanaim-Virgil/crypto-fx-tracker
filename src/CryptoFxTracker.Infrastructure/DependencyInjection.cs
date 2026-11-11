using CryptoFxTracker.Application.Abstractions;
using CryptoFxTracker.Infrastructure.Background;
using CryptoFxTracker.Infrastructure.Auth;
using CryptoFxTracker.Infrastructure.CoinGecko;
using CryptoFxTracker.Infrastructure.Notifications;
using CryptoFxTracker.Infrastructure.Persistence;
using CryptoFxTracker.Infrastructure.Tcmb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoFxTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<PriceUpdateOptions>(configuration.GetSection(PriceUpdateOptions.SectionName));
        services.AddHostedService<PriceCollectorService>();
        services.AddSingleton<PriceUpdateChannel>();
        services.AddSingleton<IAlertNotificationSender, LoggingAlertNotificationSender>();
        services.AddHostedService<AlertCheckerService>();
        services.AddAuth(configuration);

        services.AddDbContext<CryptoFxTrackerDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddHttpClient<IPriceProvider, CoinGeckoProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoFxTracker/1.0");
        });

        services.AddHttpClient<ITcmbProvider, TcmbProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoFxTracker/1.0");
        });

        return services;
    }
}
