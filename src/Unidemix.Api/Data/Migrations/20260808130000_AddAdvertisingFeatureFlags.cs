using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvertisingFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductFeatureFlags",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFeatureFlags", x => x.Key);
                });

            var createdAt = new DateTimeOffset(2026, 8, 8, 13, 0, 0, TimeSpan.Zero);
            migrationBuilder.InsertData(
                table: "ProductFeatureFlags",
                columns: new[] { "Key", "IsEnabled", "UpdatedAt" },
                values: new object[,]
                {
                    { "AdvertisingEnabled", false, createdAt },
                    { "FixedAdReservationEnabled", false, createdAt },
                    { "SocialPromotionEnabled", false, createdAt }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductFeatureFlags");
        }
    }
}
