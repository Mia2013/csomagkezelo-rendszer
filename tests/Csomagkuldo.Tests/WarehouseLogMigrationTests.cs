using Csomagkuldo.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csomagkuldo.Tests;

[TestClass]
[DoNotParallelize]
public class WarehouseLogMigrationTests
{
    [TestMethod]
    public async Task OldLogIsPreservedWithoutInventingHistoricalSpaceUsage()
    {
        await using var factory = new TestApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        factory.CheckTestDatabase(db);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261023203728_AddCourierTaskLockAndUserActivity");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO WarehouseLogs (Date, PackageCount) VALUES ('2026-10-20T10:00:00', 7);");
            await db.Database.MigrateAsync();
            var log = await db.WarehouseLogs.SingleAsync();
            Assert.AreEqual(new DateTime(2026, 10, 20, 10, 0, 0), log.Date);
            Assert.AreEqual(7, log.PackageCount);
            Assert.IsNull(log.Capacity);
            Assert.IsNull(log.UsedCapacity);
        }
        finally
        {
            factory.CheckTestDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
