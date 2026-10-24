using Csomagkuldo.Data;
using Csomagkuldo.Models;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Tests;

public static class LegacyPackageTestData
{
    // A régi sémába az új modell később hozzáadott oszlopai nélkül írunk.
    public static async Task InsertAsync(TestApplicationFactory factory, AppDbContext db, Package package)
    {
        factory.CheckTestDatabase(db);
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO Packages
                (TrackingNumber, Value, Weight, Size, IsPaid, SenderId, SenderName, SenderAddress,
                 RecipientId, RecipientName, RecipientAddress, CurrentStatus, CourierId, PickupDate, CreatedAt)
            VALUES
                ({package.TrackingNumber}, {package.Value}, {package.Weight}, {package.Size}, {package.IsPaid},
                 {package.SenderId}, {package.SenderName}, {package.SenderAddress}, {package.RecipientId},
                 {package.RecipientName}, {package.RecipientAddress}, {package.CurrentStatus.ToString()},
                 {package.CourierId}, {package.PickupDate}, {package.CreatedAt});");
        package.Id = await db.Database.SqlQuery<int>(
            $"SELECT Id AS Value FROM Packages WHERE TrackingNumber = {package.TrackingNumber}").SingleAsync();
        foreach (var history in package.StatusHistory)
        {
            history.PackageId = package.Id;
            history.Package = null!;
            db.PackageStatuses.Add(history);
        }
        await db.SaveChangesAsync();
    }
}
