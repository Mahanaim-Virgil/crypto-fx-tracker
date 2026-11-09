using CryptoFxTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoFxTracker.Infrastructure.Persistence;

public sealed class CryptoFxTrackerDbContext(DbContextOptions<CryptoFxTrackerDbContext> options)
    : DbContext(options)
{
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PriceHistory>(entity =>
        {
            entity.ToTable("PriceHistories");
            entity.HasKey(price => price.Id);
            entity.Property(price => price.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(price => price.Provider).HasMaxLength(50).IsRequired();
            entity.Property(price => price.PriceUsd).HasPrecision(20, 8);
            entity.Property(price => price.PriceTry).HasPrecision(20, 8);
            entity.Property(price => price.RecordedAtUtc).IsRequired();
            entity.HasIndex(price => new { price.Symbol, price.RecordedAtUtc });
        });
    }
}
