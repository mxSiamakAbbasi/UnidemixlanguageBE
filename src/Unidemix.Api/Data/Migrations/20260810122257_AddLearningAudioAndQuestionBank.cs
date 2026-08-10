using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningAudioAndQuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioAssetUrl",
                table: "Lessons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AudioScriptExerciseOrder",
                table: "Lessons",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioStatus",
                table: "Lessons",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CanDoObjectivesJson",
                table: "Lessons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeferredGrammarJson",
                table: "Lessons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsQuestionBankItem",
                table: "Exercises",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QuestionSkill",
                table: "Exercises",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioAssetUrl",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "AudioScriptExerciseOrder",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "AudioStatus",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "CanDoObjectivesJson",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "DeferredGrammarJson",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "IsQuestionBankItem",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "QuestionSkill",
                table: "Exercises");
        }
    }
}
