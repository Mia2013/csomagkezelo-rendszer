using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csomagkuldo.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingFeeAndCourierCarTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Size",
                table: "Packages",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "CourierCars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    CourierId = table.Column<int>(type: "int", nullable: true),
                    LicensePlate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourierCars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourierCars_AspNetUsers_CourierId",
                        column: x => x.CourierId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ShippingFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MaxSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisteredPrice = table.Column<int>(type: "int", nullable: false),
                    UnregisteredPrice = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingFees", x => x.Id);
                    table.UniqueConstraint("AK_ShippingFees_Type", x => x.Type);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Size",
                table: "Packages",
                column: "Size");

            migrationBuilder.CreateIndex(
                name: "IX_CourierCars_CourierId",
                table: "CourierCars",
                column: "CourierId");

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_ShippingFees_Size",
                table: "Packages",
                column: "Size",
                principalTable: "ShippingFees",
                principalColumn: "Type",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packages_ShippingFees_Size",
                table: "Packages");

            migrationBuilder.DropTable(
                name: "CourierCars");

            migrationBuilder.DropTable(
                name: "ShippingFees");

            migrationBuilder.DropIndex(
                name: "IX_Packages_Size",
                table: "Packages");

            migrationBuilder.AlterColumn<string>(
                name: "Size",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
