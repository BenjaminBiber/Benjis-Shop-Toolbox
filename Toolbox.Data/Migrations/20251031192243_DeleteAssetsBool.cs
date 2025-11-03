using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeleteAssetsBool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeleteAssetsOnShopRestart",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                column: "DeleteAssetsOnShopRestart",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeleteAssetsOnShopRestart",
                table: "Settings");
        }
    }
}
