using Csomagkuldo.Data;
using Csomagkuldo.Enums;
using Csomagkuldo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csomagkuldo.Tests;

[TestClass]
[DoNotParallelize]
public class PackageMigrationTests
{
    [TestMethod]
    public async Task UmlMigrationPreservesExistingPackageAndHistory()
    {
        await using var factory = new TestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        factory.CheckTestDatabase(db);

        try
        {
            // A frissítés előtti adatbázisban még szerepel a négy plusz oszlop.
            await db.GetService<IMigrator>().MigrateAsync("20261003160435_PackageBooking");
            var package = new Package
            {
                TrackingNumber = Guid.NewGuid().ToString(),
                Sender = new User { UserName = "migrationtest", Email = "migration@example.test" },
                SenderName = "Teszt Feladó",
                SenderAddress = "Veszprém, Teszt utca 1.",
                RecipientName = "Teszt Címzett",
                RecipientAddress = "Pápa, Teszt utca 2.",
                Value = 15000,
                Weight = 1200,
                Size = "M",
                PickupDate = DateTime.Today.AddDays(1)
            };
            package.StatusHistory.Add(new PackageStatusHistory { Status = PackageStatus.PreBooked });
            db.Packages.Add(package);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Packages SET SenderPhoneNumber = '+36301234567', RecipientPhoneNumber = '+36201234567', RecipientEmail = 'recipient@example.test', PickupSlot = '09:00-12:00' WHERE Id = {package.Id}");

            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();

            var saved = await db.Packages.Include(item => item.StatusHistory).SingleAsync();
            Assert.AreEqual(package.Id, saved.Id);
            Assert.AreEqual(package.TrackingNumber, saved.TrackingNumber);
            Assert.AreEqual(package.SenderId, saved.SenderId);
            Assert.AreEqual(package.SenderName, saved.SenderName);
            Assert.AreEqual(package.SenderAddress, saved.SenderAddress);
            Assert.AreEqual(package.RecipientName, saved.RecipientName);
            Assert.AreEqual(package.RecipientAddress, saved.RecipientAddress);
            Assert.AreEqual(package.Value, saved.Value);
            Assert.AreEqual(package.Weight, saved.Weight);
            Assert.AreEqual(package.Size, saved.Size);
            Assert.AreEqual(package.PickupDate, saved.PickupDate);
            Assert.AreEqual(package.CreatedAt, saved.CreatedAt);
            Assert.AreEqual(PackageStatus.PreBooked, saved.CurrentStatus);
            Assert.AreEqual(1, saved.StatusHistory.Count);
            Assert.AreEqual(package.StatusHistory.Single().Id, saved.StatusHistory.Single().Id);
            Assert.AreEqual(PackageStatus.PreBooked, saved.StatusHistory.Single().Status);

            // Az adatbázisban ténylegesen csak az UML csomagmezői maradjanak.
            await db.Database.OpenConnectionAsync();
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Packages')";
            using var reader = await command.ExecuteReaderAsync();
            var columns = new List<string>();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            CollectionAssert.AreEquivalent(new[]
            {
                "Id", "TrackingNumber", "Value", "Weight", "Size", "SenderId", "SenderName",
                "SenderAddress", "RecipientId", "RecipientName", "RecipientAddress",
                "CurrentStatus", "CourierId", "PickupDate", "CreatedAt"
            }, columns);
        }
        finally
        {
            factory.CheckTestDatabase(db);
            await db.Database.CloseConnectionAsync();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
