using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csomagkuldo.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseCapacityLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "WarehouseLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsedCapacity",
                table: "WarehouseLogs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "WarehouseLogs");

            migrationBuilder.DropColumn(
                name: "UsedCapacity",
                table: "WarehouseLogs");
        }
    }
}
