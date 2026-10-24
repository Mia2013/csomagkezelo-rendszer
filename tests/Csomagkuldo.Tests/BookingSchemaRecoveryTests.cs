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
public class BookingSchemaRecoveryTests
{
    [TestMethod]
    public async Task RestoreBookingColumnsPreservesPackageAndHistory()
    {
        await using var factory = new TestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        factory.CheckTestDatabase(db);

        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261023201842_AddPackagePaymentFlag");
            var package = new Package
            {
                TrackingNumber = Guid.NewGuid().ToString(),
                SenderName = "Teszt Feladó", SenderAddress = "Veszprém, Teszt utca 1.",
                RecipientName = "Teszt Címzett", RecipientAddress = "Pápa, Teszt utca 2.",
                Value = 1000, Weight = 500, Size = "S", PickupDate = DateTime.Today.AddDays(1)
            };
            package.StatusHistory.Add(new PackageStatusHistory { Status = PackageStatus.PreBooked });
            await LegacyPackageTestData.InsertAsync(factory, db, package);

            // Csak a saját tesztadatbázisban előállítjuk a régi ág által hagyott sémát.
            factory.CheckTestDatabase(db);
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE Packages DROP COLUMN SenderPhoneNumber, RecipientPhoneNumber, RecipientEmail, PickupSlot;
                INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
                VALUES ('20261012154919_AlignPackageWithUml', '10.0.12');
            ");

            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();
            var saved = await db.Packages.Include(item => item.StatusHistory).SingleAsync();
            Assert.AreEqual(package.Id, saved.Id);
            Assert.AreEqual(package.TrackingNumber, saved.TrackingNumber);
            Assert.AreEqual(package.SenderName, saved.SenderName);
            Assert.AreEqual(package.SenderAddress, saved.SenderAddress);
            Assert.AreEqual(package.RecipientName, saved.RecipientName);
            Assert.AreEqual(package.RecipientAddress, saved.RecipientAddress);
            Assert.AreEqual(package.Value, saved.Value);
            Assert.AreEqual(package.Weight, saved.Weight);
            Assert.AreEqual(package.Size, saved.Size);
            Assert.AreEqual(package.PickupDate, saved.PickupDate);
            Assert.AreEqual(package.CreatedAt, saved.CreatedAt);
            Assert.IsFalse(saved.IsPaid);
            Assert.AreEqual(1, saved.StatusHistory.Count);
            Assert.AreEqual(package.StatusHistory.Single().Id, saved.StatusHistory.Single().Id);

            // Az újra létrehozott mezőkbe már lehet adatot menteni.
            saved.SenderPhoneNumber = "+36301234567";
            saved.RecipientPhoneNumber = "+36201234567";
            saved.RecipientEmail = "recipient@example.test";
            saved.PickupSlot = "09:00-12:00";
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            saved = await db.Packages.SingleAsync();
            Assert.AreEqual("+36301234567", saved.SenderPhoneNumber);
            Assert.AreEqual("+36201234567", saved.RecipientPhoneNumber);
            Assert.AreEqual("recipient@example.test", saved.RecipientEmail);
            Assert.AreEqual("09:00-12:00", saved.PickupSlot);
            Assert.IsTrue((await db.Database.GetAppliedMigrationsAsync()).Contains("20261012154919_AlignPackageWithUml"));
        }
        finally
        {
            factory.CheckTestDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
