using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoFxTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersWatchlistAndNullablePriceUsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PriceUsd",
                table: "PriceHistories",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWatchlists",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWatchlists", x => new { x.UserId, x.Symbol });
                    table.ForeignKey(
                        name: "FK_UserWatchlists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWatchlists");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.AlterColumn<decimal>(
                name: "PriceUsd",
                table: "PriceHistories",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldNullable: true);
        }
    }
}
