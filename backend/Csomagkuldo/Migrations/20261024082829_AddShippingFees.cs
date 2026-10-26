using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Csomagkuldo.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShippingCost",
                table: "Packages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingFeeId",
                table: "Packages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShippingFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaxSize = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    RegisteredPrice = table.Column<int>(type: "int", nullable: false),
                    UnregisteredPrice = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingFees", x => x.Id);
                    table.CheckConstraint("CK_ShippingFees_MaxSize", "[MaxSize] IN ('S', 'M', 'L')");
                    table.CheckConstraint("CK_ShippingFees_Prices", "[RegisteredPrice] >= 0 AND [UnregisteredPrice] >= 0");
                });

            migrationBuilder.InsertData(
                table: "ShippingFees",
                columns: new[] { "Id", "MaxSize", "RegisteredPrice", "Type", "UnregisteredPrice" },
                values: new object[,]
                {
                    { 1, "S", 1000, "Small", 1000 },
                    { 2, "M", 2000, "Medium", 2000 },
                    { 3, "L", 4000, "Large", 4000 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_ShippingFeeId",
                table: "Packages",
                column: "ShippingFeeId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Packages_ShippingCost",
                table: "Packages",
                sql: "[ShippingCost] IS NULL OR [ShippingCost] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingFees_MaxSize",
                table: "ShippingFees",
                column: "MaxSize",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShippingFees_Type",
                table: "ShippingFees",
                column: "Type",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_ShippingFees_ShippingFeeId",
                table: "Packages",
                column: "ShippingFeeId",
                principalTable: "ShippingFees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packages_ShippingFees_ShippingFeeId",
                table: "Packages");

            migrationBuilder.DropTable(
                name: "ShippingFees");

            migrationBuilder.DropIndex(
                name: "IX_Packages_ShippingFeeId",
                table: "Packages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Packages_ShippingCost",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "ShippingCost",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "ShippingFeeId",
                table: "Packages");
        }
    }
}
