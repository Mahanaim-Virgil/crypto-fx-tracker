using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CryptoFxTracker.DatabaseTests;

[Collection(SqlServerCollection.Name)]
public sealed class MigrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task AllMigrations_ApplyToAnEmptySqlServer()
    {
        await using var context = fixture.CreateContext();

        var all = context.Database.GetMigrations().ToList();
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.NotEmpty(all);
        Assert.Equal(all, applied);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Model_HasNoChangesMissingFromAMigration()
    {
        // Bir entity veya DbContext ayarı değişip "dotnet ef migrations add" unutulursa bu test kırılır:
        // koddaki model ile son migration'ın snapshot'ı karşılaştırılır.
        await using var context = fixture.CreateContext();

        var snapshotModel = context.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
        Assert.NotNull(snapshotModel);
        if (snapshotModel is IMutableModel mutableModel)
        {
            snapshotModel = mutableModel.FinalizeModel();
        }

        snapshotModel = context.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel);
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(differences);
    }
}
