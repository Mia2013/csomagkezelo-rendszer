using Csomagkuldo.Data;
using Csomagkuldo.Enums;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Helpers;

public static class CourierCapacityHelper
{
    // A hívó Serializable tranzakcióban ellenőriz és menti a hozzárendelést.
    public static async Task<string?> GetAssignmentError(AppDbContext context, int courierId, string size)
    {
        int packageSpace = PackageSizeHelper.GetSpace(size);
        if (packageSpace == 0) return "A csomag mérete nem S, M vagy L.";
        var car = await context.CourierCars.AsNoTracking().FirstOrDefaultAsync(car => car.CourierId == courierId);
        if (car == null) return "A futárhoz nincs autó rendelve.";
        int? usedSpace = await GetUsedSpace(context, courierId);
        if (usedSpace == null) return "Ismeretlen csomagméret miatt a helyfoglalás nem számítható ki.";
        if (packageSpace > car.Capacity - usedSpace.Value) return "Nincs elegendő szabad hely a futár autójában.";
        return null;
    }

    // Null: egy csomag mérete hibás, ezért a helyigény nem számítható ki.
    public static async Task<int?> GetUsedSpace(AppDbContext context, int courierId)
    {
        var packages = await context.CourierTasks.AsNoTracking()
            .Where(task => task.CourierId == courierId && task.Status != CourierTaskStatus.Completed)
            .Select(task => new { task.PackageId, task.Package.Size }).Distinct().ToListAsync();

        // Régi megoldás:
        // int usedSpace = 0;
        // foreach (var package in packages)
        // {
        //     int space = PackageSizeHelper.GetSpace(package.Size);
        //     if (space == 0) return null;
        //     usedSpace += space;
        // }
        // return usedSpace;
        // Új megoldás:
        return PackageSizeHelper.GetTotalSpace(packages.Select(package => package.Size));
    }
}
