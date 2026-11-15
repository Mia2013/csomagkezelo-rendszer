using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csomagkuldo.Migrations
{
    /// <inheritdoc />
    public partial class SeedWarehouseAndAdminUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE UserName = N'raktaros') INSERT INTO AspNetUsers (UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, FirstName, LastName, Address, Role) VALUES (N'raktaros', N'RAKTAROS', N'raktaros@csomagkezelo.local', N'RAKTAROS@CSOMAGKEZELO.LOCAL', 1, N'AQAAAAIAAYagAAAAEF945+JJhOaZlzSYe8WLwdblQjgvhzcS0kuLzdKVSKyjlfBJ0ZX9lD1LXo0yfqS/iw==', 0, 0, 0, 0, N'Raktáros', N'Teszt', N'Budapest', N'WarehouseOperator');");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE UserName = N'admin') INSERT INTO AspNetUsers (UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, FirstName, LastName, Address, Role) VALUES (N'admin', N'ADMIN', N'admin@csomagkezelo.local', N'ADMIN@CSOMAGKEZELO.LOCAL', 1, N'AQAAAAIAAYagAAAAEF945+JJhOaZlzSYe8WLwdblQjgvhzcS0kuLzdKVSKyjlfBJ0ZX9lD1LXo0yfqS/iw==', 0, 0, 0, 0, N'Admin', N'Teszt', N'Budapest', N'Admin');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM AspNetUsers WHERE UserName IN (N'raktaros', N'admin') AND PasswordHash = N'AQAAAAIAAYagAAAAEF945+JJhOaZlzSYe8WLwdblQjgvhzcS0kuLzdKVSKyjlfBJ0ZX9lD1LXo0yfqS/iw==';");
        }
    }
}
