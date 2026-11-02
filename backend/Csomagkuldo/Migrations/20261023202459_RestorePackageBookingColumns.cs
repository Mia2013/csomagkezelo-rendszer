using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csomagkuldo.Migrations
{

    public partial class RestorePackageBookingColumns : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
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


        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
