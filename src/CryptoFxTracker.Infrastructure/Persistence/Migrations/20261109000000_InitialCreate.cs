using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoFxTracker.Infrastructure.Persistence.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PriceHistories",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                PriceUsd = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                PriceTry = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PriceHistories", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_PriceHistories_Symbol_RecordedAtUtc",
            table: "PriceHistories",
            columns: new[] { "Symbol", "RecordedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PriceHistories");
    }
}
