using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class _31102025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppInfo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IisRestartTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppInfo", x => x.Id);
                    table.CheckConstraint("CK_AppInfo_Singleton", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    IisAppName = table.Column<string>(type: "TEXT", nullable: true),
                    LogName = table.Column<string>(type: "TEXT", nullable: true),
                    ThemeRepositoryPath = table.Column<string>(type: "TEXT", nullable: false),
                    AutoRefreshSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoRefreshEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    OnlySinceRestart = table.Column<bool>(type: "INTEGER", nullable: false),
                    RestartShopOnThemeChange = table.Column<bool>(type: "INTEGER", nullable: false),
                    RestartDelaySeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    BundleLogs = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeleteBundlerOnShopRestart = table.Column<bool>(type: "INTEGER", nullable: false),
                    TrayIconIisSite = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.CheckConstraint("CK_AppSettings_Singleton", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "ShopDatabaseConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Server = table.Column<string>(type: "TEXT", nullable: false),
                    Database = table.Column<string>(type: "TEXT", nullable: false),
                    User = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    MaxPoolSize = table.Column<int>(type: "INTEGER", nullable: false),
                    Encrypt = table.Column<bool>(type: "INTEGER", nullable: false),
                    TrustServerCertificate = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopDatabaseConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiteId = table.Column<long>(type: "INTEGER", nullable: false),
                    ThemeFolderPath = table.Column<string>(type: "TEXT", nullable: false),
                    ShopYamlPath = table.Column<string>(type: "TEXT", nullable: false),
                    ToolboxSettingsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopSettings_Settings_ToolboxSettingsId",
                        column: x => x.ToolboxSettingsId,
                        principalTable: "Settings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppInfo",
                columns: new[] { "Id", "IisRestartTime", "StartTime" },
                values: new object[] { 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2020, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Id", "AutoRefreshEnabled", "AutoRefreshSeconds", "BundleLogs", "DeleteBundlerOnShopRestart", "IisAppName", "LogName", "OnlySinceRestart", "RestartDelaySeconds", "RestartShopOnThemeChange", "ThemeRepositoryPath", "TrayIconIisSite" },
                values: new object[] { 1, false, 60, false, false, null, "4SELLERS", true, 3, true, "C:\\Dev_Git\\KundenThemes", -9223372036854775808L });

            migrationBuilder.CreateIndex(
                name: "IX_ShopSettings_ToolboxSettingsId",
                table: "ShopSettings",
                column: "ToolboxSettingsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppInfo");

            migrationBuilder.DropTable(
                name: "ShopDatabaseConnections");

            migrationBuilder.DropTable(
                name: "ShopSettings");

            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
