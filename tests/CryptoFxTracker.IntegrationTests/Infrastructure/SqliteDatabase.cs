using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CryptoFxTracker.IntegrationTests.Infrastructure;

// Her test için bellekte, gerçek ilişkisel bir SQLite veritabanı.
// Bağlantı açık kaldıkça veritabanı yaşar; Dispose ile silinir.
// Şema migration'larla değil EnsureCreated ile kurulur: migration'lar SQL Server'a özgü.
public sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public SqliteDatabase()
    {
        connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    // Aynı veritabanına bağlı yeni bir DbContext. Ayrı context'ler, uygulamadaki ayrı scope'ları
    // (ör. API'nin bir isteği ile arka plan servisinin bir döngüsü) temsil eder.
    public CryptoFxTrackerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CryptoFxTrackerDbContext>().UseSqlite(connection).Options);

    public void Dispose() => connection.Dispose();
}
