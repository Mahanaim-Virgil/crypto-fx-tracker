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
        services.AddHostedService<AlertCheckerService>();
        AddAlertNotifications(services, configuration);
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

    // Bot token varsa Telegram, yoksa log gönderici: Telegram kurmadan da alarm akışı çalışır.
    private static void AddAlertNotifications(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(TelegramOptions.SectionName);
        // ValidateOnStart: Telegram__ChatId sayı değilse uygulama açılışta net bir hatayla durur;
        // aksi halde hata ancak ilk bildirimde, her alarm için tekrar tekrar ortaya çıkardı.
        services.AddOptions<TelegramOptions>()
            .Bind(section)
            .ValidateOnStart();

        if (string.IsNullOrWhiteSpace(section[nameof(TelegramOptions.BotToken)]))
        {
            services.AddSingleton<IAlertNotificationSender, LoggingAlertNotificationSender>();
            return;
        }

        services.AddHttpClient<IAlertNotificationSender, TelegramAlertNotificationSender>(client =>
            {
                client.BaseAddress = new Uri("https://api.telegram.org/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            // Varsayılan HttpClient logları istek adresini yazar; Telegram'da adres bot token'ını içerir.
            .RemoveAllLoggers();
    }
}
