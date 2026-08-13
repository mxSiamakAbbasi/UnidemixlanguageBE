using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTelcExamPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlueprintJson",
                table: "ExamPrograms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentVersion",
                table: "ExamPrograms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "ExamPrograms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MockVariantsJson",
                table: "ExamPrograms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PracticeBankJson",
                table: "ExamPrograms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparationDurationMinutes",
                table: "ExamPrograms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScoringJson",
                table: "ExamPrograms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "ExamPrograms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpeakingDurationMinutes",
                table: "ExamPrograms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WrittenDurationMinutes",
                table: "ExamPrograms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MockExamAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AnswersJson = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockExamAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MockExamAttempts_ExamPrograms_ExamProgramId",
                        column: x => x.ExamProgramId,
                        principalTable: "ExamPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MockExamAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MockExamAttempts_ExamProgramId",
                table: "MockExamAttempts",
                column: "ExamProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_MockExamAttempts_UserId_ExamProgramId_VariantKey_Status",
                table: "MockExamAttempts",
                columns: new[] { "UserId", "ExamProgramId", "VariantKey", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MockExamAttempts");

            migrationBuilder.DropColumn(
                name: "BlueprintJson",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "ContentVersion",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "MockVariantsJson",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "PracticeBankJson",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "PreparationDurationMinutes",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "ScoringJson",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "SpeakingDurationMinutes",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "WrittenDurationMinutes",
                table: "ExamPrograms");
        }
    }
}
