using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unidemix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialCommunityContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_ReporterId_ReportedUserId_Reason_Status",
                table: "Reports");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReportedUserId",
                table: "Reports",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                table: "Reports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "Reports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"Reports\" SET \"TargetType\" = 'User', \"TargetId\" = \"ReportedUserId\" WHERE \"ReportedUserId\" IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "SocialPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: false),
                    Context = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialPosts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostComments_SocialPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "SocialPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostLikes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostLikes_SocialPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "SocialPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostLikes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentMentions_PostComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "PostComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentMentions_SocialPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "SocialPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentMentions_Users_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId_TargetType_TargetId_Reason_Status",
                table: "Reports",
                columns: new[] { "ReporterId", "TargetType", "TargetId", "Reason", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentMentions_CommentId_MentionedUserId",
                table: "ContentMentions",
                columns: new[] { "CommentId", "MentionedUserId" },
                unique: true,
                filter: "\"CommentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContentMentions_MentionedUserId",
                table: "ContentMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentMentions_PostId_MentionedUserId",
                table: "ContentMentions",
                columns: new[] { "PostId", "MentionedUserId" },
                unique: true,
                filter: "\"PostId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostComments_PostId_Status_CreatedAt",
                table: "PostComments",
                columns: new[] { "PostId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PostComments_UserId",
                table: "PostComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostLikes_PostId",
                table: "PostLikes",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostLikes_UserId_PostId",
                table: "PostLikes",
                columns: new[] { "UserId", "PostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_Status_CreatedAt",
                table: "SocialPosts",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_UserId",
                table: "SocialPosts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentMentions");

            migrationBuilder.DropTable(
                name: "PostLikes");

            migrationBuilder.DropTable(
                name: "PostComments");

            migrationBuilder.DropTable(
                name: "SocialPosts");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReporterId_TargetType_TargetId_Reason_Status",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "Reports");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReportedUserId",
                table: "Reports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId_ReportedUserId_Reason_Status",
                table: "Reports",
                columns: new[] { "ReporterId", "ReportedUserId", "Reason", "Status" },
                unique: true);
        }
    }
}
