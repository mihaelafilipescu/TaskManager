using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Migrations
{
    /// <inheritdoc />
    public partial class CreateProjectSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectSummaries_AspNetUsers_GeneratedById",
                table: "ProjectSummaries");

            migrationBuilder.DropIndex(
                name: "IX_ProjectSummaries_GeneratedById",
                table: "ProjectSummaries");

            migrationBuilder.DropColumn(
                name: "GeneratedById",
                table: "ProjectSummaries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeneratedById",
                table: "ProjectSummaries",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSummaries_GeneratedById",
                table: "ProjectSummaries",
                column: "GeneratedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectSummaries_AspNetUsers_GeneratedById",
                table: "ProjectSummaries",
                column: "GeneratedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
