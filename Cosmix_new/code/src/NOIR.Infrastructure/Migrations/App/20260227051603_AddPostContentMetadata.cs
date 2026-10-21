using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOIR.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class AddPostContentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContentMeta_HasCodeBlocks",
                table: "Posts",
                type: "bit",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ContentMeta_HasEmbeddedMedia",
                table: "Posts",
                type: "bit",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ContentMeta_HasMathFormulas",
                table: "Posts",
                type: "bit",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ContentMeta_HasMermaidDiagrams",
                table: "Posts",
                type: "bit",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ContentMeta_HasTables",
                table: "Posts",
                type: "bit",
                nullable: true,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentMeta_HasCodeBlocks",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ContentMeta_HasEmbeddedMedia",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ContentMeta_HasMathFormulas",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ContentMeta_HasMermaidDiagrams",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ContentMeta_HasTables",
                table: "Posts");
        }
    }
}
