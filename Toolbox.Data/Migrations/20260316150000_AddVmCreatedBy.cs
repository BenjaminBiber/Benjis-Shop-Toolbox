using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toolbox.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVmCreatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "VmCustomerMappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "VmCustomerMappings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatedBy",  table: "VmCustomerMappings");
            migrationBuilder.DropColumn(name: "CreatedAt",  table: "VmCustomerMappings");
        }
    }
}
