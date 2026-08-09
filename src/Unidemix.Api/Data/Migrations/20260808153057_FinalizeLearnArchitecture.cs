using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeLearnArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedReviews",
                table: "VocabularyReviews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "VocabularyItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SectionOrderJson",
                table: "Lessons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "ExamPrograms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Courses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PathCode",
                table: "Courses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExamLevelMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    CefrLevel = table.Column<string>(type: "text", nullable: false),
                    IsApproximate = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamLevelMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamLevelMappings_ExamPrograms_ExamProgramId",
                        column: x => x.ExamProgramId,
                        principalTable: "ExamPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonSectionProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionCode = table.Column<string>(type: "text", nullable: false),
                    Percent = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonSectionProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonSectionProgress_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonSectionProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE "ExamPrograms" p
                SET "Code" = lower(replace(ep."Code" || '-' || p."Level", ' ', '-'))
                FROM "ExamProviders" ep
                WHERE ep."Id" = p."ExamProviderId";

                INSERT INTO "ExamLevelMappings" ("Id", "ExamProgramId", "CefrLevel", "IsApproximate")
                SELECT md5(p."Id"::text || ':cefr')::uuid, p."Id", p."Level", FALSE
                FROM "ExamPrograms" p;

                UPDATE "Courses"
                SET "Kind" = CASE
                    WHEN "Slug" IN ('german-at-work', 'german-for-travel', 'german-exam-b1', 'german-grammar') THEN 'Specialized'
                    ELSE 'Core'
                END,
                "PathCode" = CASE
                    WHEN "Slug" = 'german-at-work' THEN 'work'
                    WHEN "Slug" = 'german-for-travel' THEN 'travel'
                    WHEN "Slug" = 'german-exam-b1' THEN 'exam'
                    WHEN "Slug" = 'german-grammar' THEN 'grammar'
                    ELSE NULL
                END;

                UPDATE "Lessons"
                SET "SectionOrderJson" = '["vocabulary","listening","speaking","reading","writing","grammar","final-practice"]'
                WHERE "SectionOrderJson" IS NULL;

                UPDATE "VocabularyItems"
                SET "ContentType" = CASE
                    WHEN position(' ' in "Term") = 0 THEN 'Word'
                    WHEN right("Term", 1) = '.' THEN 'Expression'
                    ELSE 'Chunk'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ExamPrograms_ExamProviderId_Code",
                table: "ExamPrograms",
                columns: new[] { "ExamProviderId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamLevelMappings_ExamProgramId_CefrLevel",
                table: "ExamLevelMappings",
                columns: new[] { "ExamProgramId", "CefrLevel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonSectionProgress_LessonId",
                table: "LessonSectionProgress",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSectionProgress_UserId_LessonId_SectionCode",
                table: "LessonSectionProgress",
                columns: new[] { "UserId", "LessonId", "SectionCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamLevelMappings");

            migrationBuilder.DropTable(
                name: "LessonSectionProgress");

            migrationBuilder.DropIndex(
                name: "IX_ExamPrograms_ExamProviderId_Code",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "FailedReviews",
                table: "VocabularyReviews");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "VocabularyItems");

            migrationBuilder.DropColumn(
                name: "SectionOrderJson",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "ExamPrograms");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "PathCode",
                table: "Courses");
        }
    }
}
