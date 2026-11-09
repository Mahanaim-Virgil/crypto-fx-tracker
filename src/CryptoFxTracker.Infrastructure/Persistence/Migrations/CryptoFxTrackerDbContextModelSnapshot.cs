using System;
using CryptoFxTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace CryptoFxTracker.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CryptoFxTrackerDbContext))]
partial class CryptoFxTrackerDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.11");

        modelBuilder.Entity("CryptoFxTracker.Domain.Entities.PriceHistory", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").UseIdentityColumn();
            b.Property<decimal>("PriceTry").HasPrecision(20, 8).HasColumnType("decimal(20,8)");
            b.Property<decimal>("PriceUsd").HasPrecision(20, 8).HasColumnType("decimal(20,8)");
            b.Property<string>("Provider").HasMaxLength(50).HasColumnType("nvarchar(50)");
            b.Property<DateTimeOffset>("RecordedAtUtc").HasColumnType("datetimeoffset");
            b.Property<string>("Symbol").HasMaxLength(20).HasColumnType("nvarchar(20)");
            b.HasKey("Id");
            b.HasIndex("Symbol", "RecordedAtUtc");
            b.ToTable("PriceHistories");
        });
#pragma warning restore 612, 618
    }
}
