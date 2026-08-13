using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStrictExamAiGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedExamContentId",
                table: "MockExamAttempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExamBlueprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "text", nullable: false),
                    ExamKey = table.Column<string>(type: "text", nullable: false),
                    Variant = table.Column<string>(type: "text", nullable: false),
                    Cefr = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SourceReferencesJson = table.Column<string>(type: "text", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamBlueprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamBlueprints_ExamPrograms_ExamProgramId",
                        column: x => x.ExamProgramId,
                        principalTable: "ExamPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedExamContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamBlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    SectionKey = table.Column<string>(type: "text", nullable: true),
                    PartKey = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: true),
                    ValidatorResultJson = table.Column<string>(type: "text", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    Fingerprint = table.Column<string>(type: "text", nullable: true),
                    ModelReference = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedExamContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedExamContents_ExamBlueprints_ExamBlueprintId",
                        column: x => x.ExamBlueprintId,
                        principalTable: "ExamBlueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneratedExamContents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MockExamAttempts_GeneratedExamContentId",
                table: "MockExamAttempts",
                column: "GeneratedExamContentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamBlueprints_ExamProgramId",
                table: "ExamBlueprints",
                column: "ExamProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamBlueprints_ProviderCode_ExamKey_Variant_Version",
                table: "ExamBlueprints",
                columns: new[] { "ProviderCode", "ExamKey", "Variant", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedExamContents_ExamBlueprintId",
                table: "GeneratedExamContents",
                column: "ExamBlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedExamContents_Fingerprint",
                table: "GeneratedExamContents",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedExamContents_UserId_Status_CreatedAt",
                table: "GeneratedExamContents",
                columns: new[] { "UserId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_MockExamAttempts_GeneratedExamContents_GeneratedExamContent~",
                table: "MockExamAttempts",
                column: "GeneratedExamContentId",
                principalTable: "GeneratedExamContents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MockExamAttempts_GeneratedExamContents_GeneratedExamContent~",
                table: "MockExamAttempts");

            migrationBuilder.DropTable(
                name: "GeneratedExamContents");

            migrationBuilder.DropTable(
                name: "ExamBlueprints");

            migrationBuilder.DropIndex(
                name: "IX_MockExamAttempts_GeneratedExamContentId",
                table: "MockExamAttempts");

            migrationBuilder.DropColumn(
                name: "GeneratedExamContentId",
                table: "MockExamAttempts");
        }
    }
}
