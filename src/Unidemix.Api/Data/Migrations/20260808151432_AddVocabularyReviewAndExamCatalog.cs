using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularyReviewAndExamCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Term = table.Column<string>(type: "text", nullable: false),
                    Pronunciation = table.Column<string>(type: "text", nullable: true),
                    Meaning = table.Column<string>(type: "text", nullable: false),
                    Example = table.Column<string>(type: "text", nullable: false),
                    ExampleTranslation = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyItems_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamPrograms_ExamProviders_ExamProviderId",
                        column: x => x.ExamProviderId,
                        principalTable: "ExamProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VocabularyItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuccessfulReviews = table.Column<int>(type: "integer", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    NextReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VocabularyReviews_VocabularyItems_VocabularyItemId",
                        column: x => x.VocabularyItemId,
                        principalTable: "VocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExamSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamSections_ExamPrograms_ExamProgramId",
                        column: x => x.ExamProgramId,
                        principalTable: "ExamPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamPrograms_ExamProviderId_Level",
                table: "ExamPrograms",
                columns: new[] { "ExamProviderId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamProviders_LanguageCode_Code",
                table: "ExamProviders",
                columns: new[] { "LanguageCode", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamSections_ExamProgramId_Code",
                table: "ExamSections",
                columns: new[] { "ExamProgramId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyItems_LessonId_Order",
                table: "VocabularyItems",
                columns: new[] { "LessonId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyReviews_UserId_VocabularyItemId",
                table: "VocabularyReviews",
                columns: new[] { "UserId", "VocabularyItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyReviews_VocabularyItemId",
                table: "VocabularyReviews",
                column: "VocabularyItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamSections");

            migrationBuilder.DropTable(
                name: "VocabularyReviews");

            migrationBuilder.DropTable(
                name: "ExamPrograms");

            migrationBuilder.DropTable(
                name: "VocabularyItems");

            migrationBuilder.DropTable(
                name: "ExamProviders");
        }
    }
}
