using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGrammarLearningFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GrammarTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentKey = table.Column<string>(type: "text", nullable: false),
                    LanguageCode = table.Column<string>(type: "text", nullable: false),
                    CefrLevel = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    TitleDe = table.Column<string>(type: "text", nullable: false),
                    TitleFa = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Progression = table.Column<string>(type: "text", nullable: false),
                    ReferenceJson = table.Column<string>(type: "text", nullable: false),
                    ExercisesJson = table.Column<string>(type: "text", nullable: false),
                    RelatedCoreLessonsJson = table.Column<string>(type: "text", nullable: false),
                    ContentVersion = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarTopics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GrammarPracticeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrammarTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LanguageCode = table.Column<string>(type: "text", nullable: false),
                    CefrLevel = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    SelectedQuestionIdsJson = table.Column<string>(type: "text", nullable: false),
                    CurrentQuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarPracticeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrammarPracticeSessions_GrammarTopics_GrammarTopicId",
                        column: x => x.GrammarTopicId,
                        principalTable: "GrammarTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrammarPracticeSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrammarTopicProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrammarTopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SessionsCompleted = table.Column<int>(type: "integer", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "integer", nullable: false),
                    TotalAnswers = table.Column<int>(type: "integer", nullable: false),
                    LastPracticedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarTopicProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrammarTopicProgress_GrammarTopics_GrammarTopicId",
                        column: x => x.GrammarTopicId,
                        principalTable: "GrammarTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrammarTopicProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrammarPracticeAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrammarPracticeSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrammarTopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<string>(type: "text", nullable: false),
                    LearnerAnswer = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrammarPracticeAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrammarPracticeAnswers_GrammarPracticeSessions_GrammarPract~",
                        column: x => x.GrammarPracticeSessionId,
                        principalTable: "GrammarPracticeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GrammarPracticeAnswers_GrammarTopics_GrammarTopicId",
                        column: x => x.GrammarTopicId,
                        principalTable: "GrammarTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPracticeAnswers_GrammarPracticeSessionId_QuestionId",
                table: "GrammarPracticeAnswers",
                columns: new[] { "GrammarPracticeSessionId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPracticeAnswers_GrammarTopicId",
                table: "GrammarPracticeAnswers",
                column: "GrammarTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPracticeSessions_GrammarTopicId",
                table: "GrammarPracticeSessions",
                column: "GrammarTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarPracticeSessions_UserId_Status_UpdatedAt",
                table: "GrammarPracticeSessions",
                columns: new[] { "UserId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GrammarTopicProgress_GrammarTopicId",
                table: "GrammarTopicProgress",
                column: "GrammarTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_GrammarTopicProgress_UserId_GrammarTopicId",
                table: "GrammarTopicProgress",
                columns: new[] { "UserId", "GrammarTopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrammarTopics_ContentKey",
                table: "GrammarTopics",
                column: "ContentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrammarTopics_LanguageCode_CefrLevel_Order",
                table: "GrammarTopics",
                columns: new[] { "LanguageCode", "CefrLevel", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrammarPracticeAnswers");

            migrationBuilder.DropTable(
                name: "GrammarTopicProgress");

            migrationBuilder.DropTable(
                name: "GrammarPracticeSessions");

            migrationBuilder.DropTable(
                name: "GrammarTopics");
        }
    }
}
