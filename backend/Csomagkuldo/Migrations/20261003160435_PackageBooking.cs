using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csomagkuldo.Migrations
{
    /// <inheritdoc />
    public partial class PackageBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TrackingNumber",
                table: "Packages",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "PickupSlot",
                table: "Packages",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientEmail",
                table: "Packages",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientPhoneNumber",
                table: "Packages",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderPhoneNumber",
                table: "Packages",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_TrackingNumber",
                table: "Packages",
                column: "TrackingNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packages_TrackingNumber",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "PickupSlot",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "RecipientEmail",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "RecipientPhoneNumber",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "SenderPhoneNumber",
                table: "Packages");

            migrationBuilder.AlterColumn<string>(
                name: "TrackingNumber",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(36)",
                oldMaxLength: 36);
        }
    }
}
