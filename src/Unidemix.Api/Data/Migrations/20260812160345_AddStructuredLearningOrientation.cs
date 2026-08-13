using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredLearningOrientation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExamGoal",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoalSubtype",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LearningOnboardingCompleted",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LearningPathGuideDismissed",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TargetLevel",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Users\" SET \"LearningOnboardingCompleted\" = TRUE");
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Goal\" = CASE WHEN \"Goal\" = 'immigration' THEN 'migration' WHEN \"Goal\" = 'family' THEN 'daily-life' WHEN \"Goal\" = 'university' THEN 'study' ELSE \"Goal\" END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExamGoal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GoalSubtype",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LearningOnboardingCompleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LearningPathGuideDismissed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TargetLevel",
                table: "Users");
        }
    }
}
