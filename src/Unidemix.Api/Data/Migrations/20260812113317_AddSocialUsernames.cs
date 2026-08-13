using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialUsernames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "SocialProfiles",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.Sql("""
                WITH candidates AS (
                    SELECT sp."UserId", LEFT(TRIM(BOTH '._' FROM regexp_replace(lower(split_part(u."DisplayName", ' ', 1)), '[^a-z0-9_.]', '', 'g')), 18) AS base_name,
                           row_number() OVER (ORDER BY lower(u."DisplayName"), sp."UserId") AS global_number
                    FROM "SocialProfiles" sp JOIN "Users" u ON u."Id" = sp."UserId"
                ), ranked AS (
                    SELECT *, row_number() OVER (PARTITION BY base_name ORDER BY global_number) AS duplicate_number FROM candidates
                )
                UPDATE "SocialProfiles" sp SET "Username" = CASE
                    WHEN length(r.base_name) >= 3 AND r.base_name NOT IN ('admin','administrator','support','unidemix','system','moderator')
                        THEN LEFT(r.base_name, 20) || CASE WHEN r.duplicate_number = 1 THEN '' ELSE r.duplicate_number::text END
                    ELSE 'member' || r.global_number::text END
                FROM ranked r WHERE sp."UserId" = r."UserId";
                """);

            migrationBuilder.AlterColumn<string>(name: "Username", table: "SocialProfiles", type: "character varying(24)", maxLength: 24, nullable: false, oldClrType: typeof(string), oldType: "character varying(24)", oldMaxLength: 24, oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialProfiles_Username",
                table: "SocialProfiles",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SocialProfiles_Username",
                table: "SocialProfiles");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "SocialProfiles");
        }
    }
}
