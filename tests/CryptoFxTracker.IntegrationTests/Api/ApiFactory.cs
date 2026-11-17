using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CryptoFxTracker.Application.Models;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CryptoFxTracker.IntegrationTests.Api;

// Gerçek API'yi bellekte açar. Değişenler: rastgele JWT anahtarı, SQL Server yerine bellekte SQLite,
// arka plan servisleri kapalı (testler sırasında CoinGecko/TCMB'ye istek gitmez).
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public ApiFactory() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting, Program.cs yapılandırmayı okumadan önce uygulanır; Jwt:Secret orada okunuyor.
        builder.UseSetting("Jwt:Secret", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("Telegram:BotToken", "");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CryptoFxTrackerDbContext>>();
            services.AddDbContext<CryptoFxTrackerDbContext>(options => options.UseSqlite(connection));
            services.RemoveAll<IHostedService>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CryptoFxTrackerDbContext>().Database.EnsureCreated();
        return host;
    }

    // Yeni bir kullanıcı kaydeder, giriş yapar ve Authorization başlığı hazır bir istemci döner.
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var credentials = new LoginRequest($"user-{Guid.NewGuid():N}@example.test", "TestSifre12345");

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(credentials.Email, credentials.Password));
        register.EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", credentials);
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    // Veritabanını API'den bağımsız kontrol etmek için; scope sorgudan sonra kapanır.
    public async Task<T> QueryAsync<T>(Func<CryptoFxTrackerDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CryptoFxTrackerDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }
}
