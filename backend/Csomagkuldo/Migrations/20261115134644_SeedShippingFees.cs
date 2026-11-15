using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csomagkuldo.Migrations
{
    /// <inheritdoc />
    public partial class SeedShippingFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM ShippingFees WHERE [Type] = 'S') INSERT INTO ShippingFees ([Type], MaxSize, RegisteredPrice, UnregisteredPrice, CapacityUnit) VALUES ('S', '30x20x10 cm', 1000, 1000, 1);");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM ShippingFees WHERE [Type] = 'M') INSERT INTO ShippingFees ([Type], MaxSize, RegisteredPrice, UnregisteredPrice, CapacityUnit) VALUES ('M', '40x30x20 cm', 2000, 2000, 2);");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM ShippingFees WHERE [Type] = 'L') INSERT INTO ShippingFees ([Type], MaxSize, RegisteredPrice, UnregisteredPrice, CapacityUnit) VALUES ('L', '60x40x30 cm', 3000, 3000, 4);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
