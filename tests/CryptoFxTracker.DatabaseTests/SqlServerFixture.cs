using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CryptoFxTracker.DatabaseTests;

// Bütün veritabanı testleri tek bir SQL Server container'ını paylaşır (açılması 10-20 sn sürer).
// Container açılınca projenin gerçek migration'ları uygulanır; testler EnsureCreated değil, migration'ların
// ürettiği şemayla çalışır.
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        // Testcontainers "master" veritabanını verir; uygulama tablolarını ayrı bir veritabanına kuruyoruz.
        ConnectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = "CryptoFxTrackerTests"
        }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    public CryptoFxTrackerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CryptoFxTrackerDbContext>().UseSqlServer(ConnectionString).Options);
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
