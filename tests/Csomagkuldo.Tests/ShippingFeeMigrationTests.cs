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
public class ShippingFeeMigrationTests
{
    [TestMethod]
    public async Task MigrationPreservesOldDataSeedsTariffsAndSupportsManyPackagesPerTariff()
    {
        await using var factory = new TestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        factory.CheckTestDatabase(db);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261023213349_AddCourierCars");
            var old = new Package
            {
                TrackingNumber = Guid.NewGuid().ToString(), Size = "M", Value = 12345,
                CurrentStatus = PackageStatus.Delivered, IsPaid = true
            };
            old.StatusHistory.Add(new PackageStatusHistory { Status = PackageStatus.Delivered });
            await LegacyPackageTestData.InsertAsync(factory, db, old);
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();
            var saved = await db.Packages.Include(package => package.StatusHistory).SingleAsync();
            Assert.AreEqual(old.Id, saved.Id);
            Assert.AreEqual(old.TrackingNumber, saved.TrackingNumber);
            Assert.AreEqual(old.Value, saved.Value);
            Assert.AreEqual(PackageStatus.Delivered, saved.CurrentStatus);
            Assert.IsTrue(saved.IsPaid);
            Assert.IsNull(saved.ShippingFeeId);
            Assert.IsNull(saved.ShippingCost);
            Assert.AreEqual(old.StatusHistory[0].Id, saved.StatusHistory.Single().Id);
            var fees = await db.ShippingFees.OrderBy(fee => fee.Id).ToListAsync();
            CollectionAssert.AreEqual(new[] { "S", "M", "L" }, fees.Select(fee => fee.MaxSize).ToArray());
            CollectionAssert.AreEqual(new[] { ShippingFeeType.Small, ShippingFeeType.Medium, ShippingFeeType.Large }, fees.Select(fee => fee.Type).ToArray());
            CollectionAssert.AreEqual(new[] { 1000, 2000, 4000 }, fees.Select(fee => fee.RegisteredPrice).ToArray());
            CollectionAssert.AreEqual(new[] { 1000, 2000, 4000 }, fees.Select(fee => fee.UnregisteredPrice).ToArray());
            db.Packages.AddRange(
                new Package { TrackingNumber = Guid.NewGuid().ToString(), Size = "S", ShippingFeeId = fees[0].Id, ShippingCost = 1000 },
                new Package { TrackingNumber = Guid.NewGuid().ToString(), Size = "S", ShippingFeeId = fees[0].Id, ShippingCost = 1000 });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var feeWithPackages = await db.ShippingFees.Include(fee => fee.Packages).SingleAsync(fee => fee.MaxSize == "S");
            Assert.AreEqual(2, feeWithPackages.Packages.Count);
            await Assert.ThrowsExceptionAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ShippingFees WHERE Id = {feeWithPackages.Id}"));
            await db.Database.MigrateAsync();
            Assert.AreEqual(3, await db.ShippingFees.CountAsync());
        }
        finally
        {
            factory.CheckTestDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [DataTestMethod]
    [DataRow("registered")]
    [DataRow("unregistered")]
    [DataRow("cost")]
    public async Task DatabaseRejectsNegativeTariffAndStoredPrice(string field)
    {
        await using var factory = new TestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        factory.CheckTestDatabase(db);
        try
        {
            await db.Database.MigrateAsync();
            var fee = await db.ShippingFees.SingleAsync(fee => fee.MaxSize == "S");
            if (field == "registered") fee.RegisteredPrice = -1;
            if (field == "unregistered") fee.UnregisteredPrice = -1;
            if (field == "cost")
                db.Packages.Add(new Package
                {
                    TrackingNumber = Guid.NewGuid().ToString(), Size = "S", ShippingFeeId = fee.Id, ShippingCost = -1
                });
            await Assert.ThrowsExceptionAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            var saved = await db.ShippingFees.SingleAsync(fee => fee.MaxSize == "S");
            Assert.AreEqual(1000, saved.RegisteredPrice);
            Assert.AreEqual(1000, saved.UnregisteredPrice);
            Assert.AreEqual(0, await db.Packages.CountAsync());
        }
        finally
        {
            factory.CheckTestDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
