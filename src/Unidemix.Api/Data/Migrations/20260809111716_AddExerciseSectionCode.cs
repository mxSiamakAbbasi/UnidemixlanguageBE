using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseSectionCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SectionCode",
                table: "Exercises",
                type: "text",
                nullable: false,
                defaultValue: "grammar");

            migrationBuilder.Sql("""
                UPDATE "Exercises"
                SET "SectionCode" = CASE
                    WHEN "Type" = 'listening' THEN 'listening'
                    WHEN "Type" = 'translation' THEN 'writing'
                    ELSE 'grammar'
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SectionCode",
                table: "Exercises");
        }
    }
}
