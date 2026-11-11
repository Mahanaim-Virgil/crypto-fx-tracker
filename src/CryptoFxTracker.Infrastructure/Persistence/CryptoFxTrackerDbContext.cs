using CryptoFxTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoFxTracker.Infrastructure.Persistence;

public sealed class CryptoFxTrackerDbContext(DbContextOptions<CryptoFxTrackerDbContext> options)
    : DbContext(options)
{
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserWatchlist> UserWatchlists => Set<UserWatchlist>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PriceHistory>(entity =>
        {
            entity.ToTable("PriceHistories");
            entity.HasKey(price => price.Id);
            entity.Property(price => price.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(price => price.Provider).HasMaxLength(50).IsRequired();
            entity.Property(price => price.PriceUsd).HasPrecision(20, 8).IsRequired(false);
            entity.Property(price => price.PriceTry).HasPrecision(20, 8);
            entity.Property(price => price.RecordedAtUtc).IsRequired();
            entity.HasIndex(price => new { price.Symbol, price.RecordedAtUtc });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<UserWatchlist>(entity =>
        {
            entity.ToTable("UserWatchlists");
            entity.HasKey(item => new { item.UserId, item.Symbol });
            entity.Property(item => item.Symbol).HasMaxLength(20).IsRequired();
            entity.HasOne(item => item.User)
                .WithMany(user => user.Watchlist)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PriceAlert>(entity =>
        {
            entity.ToTable("PriceAlerts");
            entity.HasKey(alert => alert.Id);
            entity.Property(alert => alert.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(alert => alert.Condition).HasConversion<string>().HasMaxLength(10);
            entity.Property(alert => alert.TargetPriceTry).HasPrecision(20, 8);
            entity.Property(alert => alert.TriggeredPriceTry).HasPrecision(20, 8);
            entity.HasIndex(alert => new { alert.Symbol, alert.IsActive });
            entity.HasOne(alert => alert.User)
                .WithMany(user => user.PriceAlerts)
                .HasForeignKey(alert => alert.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
