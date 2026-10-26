using Csomagkuldo.Data;
using Csomagkuldo.Enums;
using Csomagkuldo.Models;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Services;

public class PackageService
{
    private readonly AppDbContext _context;

    public PackageService(AppDbContext context)
    {
        _context = context;
    }

    // Régi megoldás:
    // // Hibánál a hívó visszaadja az Error értékét, siker esetén használja a két objektumot.
    // private async Task<(Package? Package, User? Courier, IActionResult? Error)>
    //     GetPackageAndCourier(int packageId, int courierId)
    // {
    //     var package = await _context.Packages.FindAsync(packageId);
    //     if (package == null)
    //         return (null, null, NotFound("Csomag nem található"));
    //
    //     var courier = await _context.Users.FindAsync(courierId);
    //     if (courier == null)
    //         return (package, null, NotFound("Futár nem található"));
    //
    //     return (package, courier, null);
    // }
    // Új megoldás:
    // Hibánál a hívó visszaadja az Error értékét, siker esetén használja a két objektumot.
    public async Task<(Package? Package, User? Courier, string? Error)>
        GetPackageAndCourier(int packageId, int courierId)
    {
        var package = await _context.Packages.FindAsync(packageId);
        if (package == null)
            return (null, null, "Csomag nem található");

        var courier = await _context.Users.FindAsync(courierId);
        if (courier == null)
            return (package, null, "Futár nem található");

        return (package, courier, null);
    }

    // Az előzetes ellenőrzés és a mentés a hívó művelet feladata.
    // Régi megoldás:
    // private void AssignPackage(Package package, int courierId, CourierTaskType taskType, DateTime assignedAt)
    // Új megoldás:
    public void AssignPackage(Package package, int courierId, CourierTaskType taskType, DateTime assignedAt)
    {
        package.CourierId = courierId;
        _context.CourierTasks.Add(new CourierTask
        {
            Package = package,
            CourierId = courierId,
            TaskType = taskType,
            Status = CourierTaskStatus.Assigned,
            AssignmentTime = assignedAt
        });
        PackageStatus status = taskType == CourierTaskType.Pickup
            ? PackageStatus.AssignedForPickup
            : PackageStatus.AssignedForDelivery;
        SetPackageStatus(package, status, courierId, assignedAt);
    }

    // Régi megoldás:
    // private void SetPackageStatus(Package package, PackageStatus status, int courierId, DateTime changedAt)
    // Új megoldás:
    public void SetPackageStatus(Package package, PackageStatus status, int courierId, DateTime changedAt)
    {
        package.CurrentStatus = status;
        _context.PackageStatuses.Add(new PackageStatusHistory
        {
            Package = package,
            CourierId = courierId,
            Status = status,
            ChangedAt = changedAt
        });
    }
}
