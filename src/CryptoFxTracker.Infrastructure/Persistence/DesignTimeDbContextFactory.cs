using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CryptoFxTracker.Infrastructure.Persistence;

// dotnet ef (migrations add, database update, migrations bundle) DbContext'i bu sınıfla oluşturur.
// Böylece Program.cs çalışmaz ve onun istediği Jwt__Secret gibi ayarlar gerekmez; Docker'da
// migration bundle'ı derlerken imaja sahte bir anahtar koymak gerekmez.
// Takas: varsayılan bağlantı dizesi appsettings.json'dakinin kopyasıdır.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CryptoFxTrackerDbContext>
{
    private const string LocalDbConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=CryptoFxTracker;Trusted_Connection=True;TrustServerCertificate=True";

    public CryptoFxTrackerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? LocalDbConnectionString;
        return new CryptoFxTrackerDbContext(
            new DbContextOptionsBuilder<CryptoFxTrackerDbContext>().UseSqlServer(connectionString).Options);
    }
}
