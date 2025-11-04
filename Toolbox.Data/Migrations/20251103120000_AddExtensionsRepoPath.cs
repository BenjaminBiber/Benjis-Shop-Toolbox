using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toolbox.Data.Migrations
{
    public partial class AddExtensionsRepoPath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtensionsRepositoryPath",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "C:\\Dev_Git\\Extensions");

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                column: "ExtensionsRepositoryPath",
                value: "C:\\Dev_Git\\Extensions");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtensionsRepositoryPath",
                table: "Settings");
        }
    }
}

