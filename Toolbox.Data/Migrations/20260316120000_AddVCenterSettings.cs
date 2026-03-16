using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVCenterSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VCenterUrl",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VCenterUsername",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VCenterPassword",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VCenterIgnoreSslErrors",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "VCenterUrl", "VCenterUsername", "VCenterPassword", "VCenterIgnoreSslErrors" },
                values: new object[] { null, null, null, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VCenterUrl", table: "Settings");
            migrationBuilder.DropColumn(name: "VCenterUsername", table: "Settings");
            migrationBuilder.DropColumn(name: "VCenterPassword", table: "Settings");
            migrationBuilder.DropColumn(name: "VCenterIgnoreSslErrors", table: "Settings");
        }
    }
}
