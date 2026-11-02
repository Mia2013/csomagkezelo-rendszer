using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csomagkuldo.Migrations
{

    public partial class AddWarehouseCapacityLog : Migration
    {

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
