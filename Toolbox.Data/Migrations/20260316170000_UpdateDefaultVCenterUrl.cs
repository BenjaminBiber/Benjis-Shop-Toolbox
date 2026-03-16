using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDefaultVCenterUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only set the default for rows that haven't been configured yet
            migrationBuilder.Sql(
                "UPDATE Settings SET VCenterUrl = 'https://staging-vc-1.logic-base.local' WHERE Id = 1 AND (VCenterUrl IS NULL OR VCenterUrl = '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Settings SET VCenterUrl = NULL WHERE Id = 1 AND VCenterUrl = 'https://staging-vc-1.logic-base.local'");
        }
    }
}
