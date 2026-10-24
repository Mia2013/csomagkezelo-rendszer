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
public class CourierSafetyMigrationTests
{
    [TestMethod]
    public async Task MigrationPreservesUsersAndLocksPreviouslyStartedTasks()
    {
        await using var factory = new TestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        factory.CheckTestDatabase(db);

        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261023202459_RestorePackageBookingColumns");

            // A régi sémában még nincs IsActive és IsLocked oszlop.
            await db.Database.ExecuteSqlRawAsync(@"
                INSERT INTO AspNetUsers
                    (UserName, FirstName, LastName, Address, Role, EmailConfirmed,
                     PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
                VALUES ('migration-courier', 'Teszt', 'Futar', 'Veszprem', 'Courier', 0, 0, 0, 0, 0);
            ");
            int courierId = await db.Database.SqlQueryRaw<int>(
                "SELECT Id AS Value FROM AspNetUsers WHERE UserName = 'migration-courier'").SingleAsync();
            var package = new Package
            {
                TrackingNumber = Guid.NewGuid().ToString(),
                SenderName = "Teszt Feladó", SenderAddress = "Veszprém, Teszt utca 1.",
                RecipientName = "Teszt Címzett", RecipientAddress = "Pápa, Teszt utca 2.",
                Value = 1000, Weight = 500, Size = "S", PickupDate = DateTime.Today.AddDays(1)
            };
            package.StatusHistory.Add(new PackageStatusHistory { Status = PackageStatus.PreBooked });
            await LegacyPackageTestData.InsertAsync(factory, db, package);

            await db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO CourierTasks (CourierId, PackageId, TaskType, AssignmentTime, Status)
                VALUES ({courierId}, {package.Id}, 'Pickup', SYSUTCDATETIME(), 'Assigned'),
                       ({courierId}, {package.Id}, 'Pickup', SYSUTCDATETIME(), 'InProgress'),
                       ({courierId}, {package.Id}, 'Delivery', SYSUTCDATETIME(), 'Completed');
            ");
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();

            var courier = await db.Users.SingleAsync();
            Assert.AreEqual(courierId, courier.Id);
            Assert.AreEqual("migration-courier", courier.UserName);
            Assert.AreEqual(UserRole.Courier, courier.Role);
            Assert.IsTrue(courier.IsActive);
            var tasks = await db.CourierTasks.OrderBy(task => task.Id).ToListAsync();
            Assert.AreEqual(3, tasks.Count);
            Assert.IsFalse(tasks[0].IsLocked);
            Assert.IsTrue(tasks[1].IsLocked);
            Assert.IsTrue(tasks[2].IsLocked);
            Assert.AreEqual(CourierTaskStatus.Assigned, tasks[0].Status);
            Assert.AreEqual(CourierTaskStatus.InProgress, tasks[1].Status);
            Assert.AreEqual(CourierTaskStatus.Completed, tasks[2].Status);
            Assert.IsTrue(tasks.All(task => task.CourierId == courierId && task.PackageId == package.Id));
            Assert.AreEqual(1, await db.PackageStatuses.CountAsync());
            Assert.AreEqual(package.TrackingNumber, (await db.Packages.SingleAsync()).TrackingNumber);

            // A meglévő felhasználó később inaktívvá is tehető.
            courier.IsActive = false;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.IsFalse((await db.Users.SingleAsync()).IsActive);
        }
        finally
        {
            factory.CheckTestDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
