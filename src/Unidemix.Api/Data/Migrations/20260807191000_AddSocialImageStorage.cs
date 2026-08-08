using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260807191000_AddSocialImageStorage")]
public sealed class AddSocialImageStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "ImageStorageKey", table: "SocialPosts", type: "text", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "ImageStorageKey", table: "SocialPosts");
}
