using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Unidemix.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260808120000_AddSocialDiscoveryProfileRefinement")]
public sealed class AddSocialDiscoveryProfileRefinement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "CityId", table: "SocialProfiles", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProfilePhotoStorageKey", table: "SocialProfiles", type: "text", nullable: true);
        migrationBuilder.CreateTable(name: "Cities", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false), CountryCode = table.Column<string>(type: "text", nullable: false),
            CanonicalName = table.Column<string>(type: "text", nullable: false), PersianName = table.Column<string>(type: "text", nullable: false),
            EnglishName = table.Column<string>(type: "text", nullable: false), GermanName = table.Column<string>(type: "text", nullable: true),
            LocalName = table.Column<string>(type: "text", nullable: true), SearchAliases = table.Column<string>(type: "text", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_Cities", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_SocialProfiles_CityId", table: "SocialProfiles", column: "CityId");
        migrationBuilder.CreateIndex(name: "IX_Cities_CountryCode_CanonicalName", table: "Cities", columns: new[] { "CountryCode", "CanonicalName" }, unique: true);
        migrationBuilder.AddForeignKey(name: "FK_SocialProfiles_Cities_CityId", table: "SocialProfiles", column: "CityId", principalTable: "Cities", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_SocialProfiles_Cities_CityId", table: "SocialProfiles");
        migrationBuilder.DropTable(name: "Cities"); migrationBuilder.DropIndex(name: "IX_SocialProfiles_CityId", table: "SocialProfiles");
        migrationBuilder.DropColumn(name: "CityId", table: "SocialProfiles"); migrationBuilder.DropColumn(name: "ProfilePhotoStorageKey", table: "SocialProfiles");
    }
}
