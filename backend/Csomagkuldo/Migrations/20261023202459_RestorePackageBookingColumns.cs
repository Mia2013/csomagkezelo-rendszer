using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csomagkuldo.Migrations
{
    /// <inheritdoc />
    public partial class RestorePackageBookingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Egy korábbi ág eltávolította ezeket a már ismét szükséges mezőket.
            // Friss adatbázisban a PackageBooking már létrehozta őket.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('dbo.Packages', 'SenderPhoneNumber') IS NULL
                    ALTER TABLE dbo.Packages ADD SenderPhoneNumber nvarchar(30) NULL;
                IF COL_LENGTH('dbo.Packages', 'RecipientPhoneNumber') IS NULL
                    ALTER TABLE dbo.Packages ADD RecipientPhoneNumber nvarchar(30) NULL;
                IF COL_LENGTH('dbo.Packages', 'RecipientEmail') IS NULL
                    ALTER TABLE dbo.Packages ADD RecipientEmail nvarchar(254) NULL;
                IF COL_LENGTH('dbo.Packages', 'PickupSlot') IS NULL
                    ALTER TABLE dbo.Packages ADD PickupSlot nvarchar(11) NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ezek az oszlopok a PackageBooking migrációhoz tartoznak.
            // A javítás visszavonása nem törli őket és az adataikat.
        }
    }
}
