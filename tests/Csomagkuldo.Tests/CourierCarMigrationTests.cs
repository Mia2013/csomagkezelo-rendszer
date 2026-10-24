using Csomagkuldo.Data;
using Csomagkuldo.Enums;
using Csomagkuldo.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csomagkuldo.Tests;

[TestClass]
[DoNotParallelize]
public class CourierCarMigrationTests
{
    [TestMethod]
    public async Task MigrationPreservesExistingDataAndEnforcesOneCarPerCourier()
    {
        await using var factory = new TestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        factory.CheckTestDatabase(db);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261023210221_AddWarehouseCapacityLog");
            var courier = new User { UserName = "old-courier", FirstName = "Régi", Role = UserRole.Courier };
            db.Users.Add(courier);
            await db.SaveChangesAsync();
            var package = new Package { TrackingNumber = Guid.NewGuid().ToString(), Size = "M", CurrentStatus = PackageStatus.AssignedForPickup };
            var history = new PackageStatusHistory { Courier = courier, Status = PackageStatus.AssignedForPickup };
            package.StatusHistory.Add(history);
            await LegacyPackageTestData.InsertAsync(factory, db, package);
            var task = new CourierTask { Courier = courier, PackageId = package.Id, IsLocked = true };
            db.CourierTasks.Add(task);
            await db.SaveChangesAsync();
            int taskId = task.Id;
            int historyId = history.Id;
            db.ChangeTracker.Clear();

            await db.Database.MigrateAsync();
            Assert.AreEqual(0, await db.CourierCars.CountAsync());
            Assert.AreEqual("Régi", (await db.Users.SingleAsync()).FirstName);
            Assert.AreEqual(package.TrackingNumber, (await db.Packages.SingleAsync()).TrackingNumber);
            var savedTask = await db.CourierTasks.SingleAsync();
            Assert.AreEqual(taskId, savedTask.Id);
            Assert.AreEqual(courier.Id, savedTask.CourierId);
            Assert.IsTrue(savedTask.IsLocked);
            Assert.AreEqual(historyId, (await db.PackageStatuses.SingleAsync()).Id);

            // Több szabad autó lehet, de ugyanaz a futár csak egyet kaphat.
            var firstCar = new CourierCar { LicensePlate = "ABC-123", Capacity = 6 };
            var secondCar = new CourierCar { LicensePlate = "DEF-456", Capacity = 9 };
            db.CourierCars.AddRange(firstCar, secondCar);
            await db.SaveChangesAsync();
            firstCar.CourierId = courier.Id;
            await db.SaveChangesAsync();
            await Assert.ThrowsExceptionAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CourierCars SET CourierId = {courier.Id} WHERE Id = {secondCar.Id}"));
            db.ChangeTracker.Clear();
            var savedCourier = await db.Users.Include(user => user.CourierCar).SingleAsync();
            Assert.AreEqual(firstCar.Id, savedCourier.CourierCar!.Id);
            Assert.IsNull((await db.CourierCars.FindAsync(secondCar.Id))!.CourierId);
        }
        finally
        {
            factory.CheckTestDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
