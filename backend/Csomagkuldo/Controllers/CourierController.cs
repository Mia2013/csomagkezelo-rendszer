using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Csomagkuldo.Enums;
using Csomagkuldo.Helpers;
using Csomagkuldo.Models;
using Csomagkuldo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Csomagkuldo.Controllers;

[Route("api/Package")]
[ApiController]
public class CourierController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PackageService _packageService;
    private readonly int _warehouseCapacity;

    public CourierController(AppDbContext context, PackageService packageService, IConfiguration configuration)
    {
        _context = context;
        _packageService = packageService;
        _warehouseCapacity = configuration.GetValue<int>("Warehouse:Capacity");
    }

    [Authorize(Roles = "Courier,Admin")]
    [HttpPut("{packageid}/startpickup")]
    public async Task<IActionResult> StartPickup(int packageid, [FromBody] PackageDTO dto)
    {
        if (dto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        if (!User.IsInRole("Admin") && User.FindFirst("id")?.Value != dto.CourierId.ToString()) return Forbid();

        // Régi megoldás a közös lekérő előtt:
        // var package = await _context.Packages.FindAsync(packageid);
        // var courier = await _context.Users.FindAsync(dto.CourierId);
        //
        // if (package == null) return NotFound("A csomag nem található!");
        //
        // if (courier == null) return NotFound("A futár nem található!");
        // Új megoldás:
        // Régi megoldás:
        // var result = await GetPackageAndCourier(packageid, dto.CourierId.Value);
        // if (result.Error != null) return result.Error;
        // Új megoldás:
        var result = await _packageService.GetPackageAndCourier(packageid, dto.CourierId.Value);
        if (result.Error != null) return NotFound(result.Error);

        var package = result.Package!;
        var courier = result.Courier!;

        var task = await _context.CourierTasks.FirstOrDefaultAsync(task => task.PackageId == packageid &&
            task.CourierId == dto.CourierId && task.TaskType == CourierTaskType.Pickup && task.Status == CourierTaskStatus.Assigned);

        if (task == null) return NotFound("A kiosztott felvételi feladat nem található!");

        if (package.CurrentStatus != PackageStatus.AssignedForPickup || package.CourierId != courier.Id || task.IsLocked)
            return Conflict("A felvételi feladat nem kezdhető meg.");

        task.Status = CourierTaskStatus.InProgress;
        task.IsLocked = true;

        await _context.SaveChangesAsync();
        return Ok("A felvételi feladat megkezdve.");
    }

    // A futár átveszi a csomagot a feladótól.
    [Authorize(Roles = "Courier,Admin")]
    [HttpPut("{packageid}/pickuppackage")]
    public async Task<IActionResult> Pickup(int packageid, [FromBody] PackageDTO dto)
    {
        if (dto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        if (!User.IsInRole("Admin") && User.FindFirst("id")?.Value != dto.CourierId.ToString()) return Forbid();

        // Régi megoldás a közös lekérő előtt:
        // // Régi megoldás:
        // // var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
        // // Új megoldás:
        // var package = await _context.Packages.FindAsync(packageid);
        // // Régi megoldás:
        // // var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == dto.CourierId);
        // // Új megoldás:
        // var courier = await _context.Users.FindAsync(dto.CourierId);
        //
        // if (package == null)
        // {
        //     return NotFound("Csomag nem található");
        // }
        //
        // if (courier == null)
        // {
        //     return NotFound("Futár nem található");
        // }
        // Új megoldás:
        // Régi megoldás:
        // var result = await GetPackageAndCourier(packageid, dto.CourierId.Value);
        // if (result.Error != null) return result.Error;
        // Új megoldás:
        var result = await _packageService.GetPackageAndCourier(packageid, dto.CourierId.Value);
        if (result.Error != null) return NotFound(result.Error);

        var package = result.Package!;
        var courier = result.Courier!;

        var couriertask = await _context.CourierTasks.FirstOrDefaultAsync(task =>
            task.PackageId == packageid && task.CourierId == dto.CourierId &&
            task.TaskType == CourierTaskType.Pickup && task.Status == CourierTaskStatus.InProgress);
        if (couriertask == null) return NotFound("Előbb kezdd meg a felvételi feladatot!");
        if (package.CurrentStatus != PackageStatus.AssignedForPickup || package.CourierId != courier.Id || !couriertask.IsLocked)
            return Conflict("Csak az adott futárhoz felvételre kiosztott csomag vehető fel.");

        // Régi megoldás:
        // SetPackageStatus(package, PackageStatus.InTransitToWarehouse, courier.Id, DateTime.UtcNow);
        // Új megoldás:
        _packageService.SetPackageStatus(package, PackageStatus.InTransitToWarehouse, courier.Id, DateTime.UtcNow);

        await _context.SaveChangesAsync();
        return Ok("Csomag sikeresen felvéve!");
    }

    // A futár a felvételre váró feladatait látja.
    [Authorize(Roles = "Courier,Admin")]
    [HttpGet("{courierid}/pickuptasks")]
    [ProducesResponseType(typeof(List<CourierTaskDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourirerPickUpTasks(int courierId)
    {
        if (!User.IsInRole("Admin") && User.FindFirst("id")?.Value != courierId.ToString()) return Forbid();

        var courier = await _context.Users.FindAsync(courierId);
        if (courier == null)
        {
            return NotFound("Futár nem található!");
        }
        if (courier.Role != UserRole.Courier) return BadRequest("A felhasználó nem futár.");

        var tasks = _context.CourierTasks.AsNoTracking()
            .Where(task => task.CourierId == courierId && task.Package.CourierId == courierId &&
                task.TaskType == CourierTaskType.Pickup && task.Status != CourierTaskStatus.Completed &&
                task.Package.CurrentStatus == PackageStatus.AssignedForPickup);
        var result = await GetCourierTaskList(tasks);

        return Ok(result);
    }

    // A futár a várakozó és a folyamatban lévő kiszállításait is látja.
    [Authorize(Roles = "Courier,Admin")]
    [HttpGet("{courierid}/deliverytasks")]
    [ProducesResponseType(typeof(List<CourierTaskDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourirerDeiveryTasks(int courierId)
    {
        if (!User.IsInRole("Admin") && User.FindFirst("id")?.Value != courierId.ToString()) return Forbid();

        var courier = await _context.Users.FindAsync(courierId);
        if (courier == null)
        {
            return NotFound("Futár nem található!");
        }
        if (courier.Role != UserRole.Courier) return BadRequest("A felhasználó nem futár.");
        var tasks = _context.CourierTasks.AsNoTracking()
            .Where(task => task.CourierId == courierId && task.Package.CourierId == courierId &&
                task.TaskType == CourierTaskType.Delivery &&
                ((task.Status == CourierTaskStatus.Assigned && task.Package.CurrentStatus == PackageStatus.AssignedForDelivery) ||
                 (task.Status == CourierTaskStatus.InProgress && task.Package.CurrentStatus == PackageStatus.OutForDelivery)));
        var result = await GetCourierTaskList(tasks);

        return Ok(result);
    }

    [Authorize(Roles = "Courier,Admin")]
    [HttpPut("{packageid}/startdelivery")]
    public async Task<IActionResult> StartDelivery(int packageid, [FromBody] PackageDTO dto)
    {
        if (dto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        if (!User.IsInRole("Admin") && User.FindFirst("id")?.Value != dto.CourierId.ToString()) return Forbid();
        if (_warehouseCapacity <= 0) return Problem("A raktár kapacitása nincs megfelelően beállítva.");

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Régi megoldás a közös lekérő előtt:
        // var package = await _context.Packages.FindAsync(packageid);
        // var courier = await _context.Users.FindAsync(dto.CourierId);
        //
        // if (package == null) return NotFound("A csomag nem található.");
        //
        // if (courier == null) return NotFound("A futár nem található.");
        // Új megoldás:
        // Régi megoldás:
        // var result = await GetPackageAndCourier(packageid, dto.CourierId.Value);
        // if (result.Error != null) return result.Error;
        // Új megoldás:
        var result = await _packageService.GetPackageAndCourier(packageid, dto.CourierId.Value);
        if (result.Error != null) return NotFound(result.Error);

        var package = result.Package!;
        var courier = result.Courier!;

        if (courier.Role != UserRole.Courier) return BadRequest("A felhasználó nem futár.");
        var tasks = await _context.CourierTasks
            .Where(task => task.PackageId == packageid && task.Status != CourierTaskStatus.Completed).ToListAsync();

        if (tasks.Count == 0) return NotFound("Nincs aktív kiszállítási feladat.");

        if (tasks.Count != 1) return Conflict("A csomaghoz több aktív feladat tartozik.");

        var task = tasks[0];

        if (task.CourierId != courier.Id) return Forbid();
        //ez kicsit csúnya, lehet segéd függvénybe kellene szervezni
        if (task.TaskType != CourierTaskType.Delivery || task.Status != CourierTaskStatus.Assigned || task.IsLocked ||
            package.CurrentStatus != PackageStatus.AssignedForDelivery || package.CourierId != courier.Id)
            return Conflict("Csak a futárhoz kiszállításra kiosztott, még meg nem kezdett feladat indítható.");

        // Az induló csomag kikerül a fizikai raktárkészletből, az autóhelye továbbra is foglalt.
        var sizes = await _context.Packages.AsNoTracking()
            .Where(p => p.Id != packageid &&
                (p.CurrentStatus == PackageStatus.AtWarehouse || p.CurrentStatus == PackageStatus.AssignedForDelivery))
            .Select(p => p.Size).ToListAsync();

        if (PackageSizeHelper.GetSpace(package.Size) == 0) return Conflict("A csomag mérete nem S, M vagy L.");

        // Régi megoldás:
        // int usedCapacity = 0;
        //
        // foreach (string size in sizes)
        // {
        //     int space = PackageSizeHelper.GetSpace(size);
        //
        //     if (space == 0) return Conflict("A raktárban ismeretlen méretű csomag található. Előbb javítani kell az adatát.");
        //
        //     usedCapacity += space;
        // }
        // Új megoldás:
        int? warehouseSpace = PackageSizeHelper.GetTotalSpace(sizes);
        if (warehouseSpace == null) return Conflict("A raktárban ismeretlen méretű csomag található. Előbb javítani kell az adatát.");
        int usedCapacity = warehouseSpace.Value;

        DateTime now = DateTime.UtcNow;
        task.Status = CourierTaskStatus.InProgress;
        task.IsLocked = true;

        // Régi megoldás:
        // SetPackageStatus(package, PackageStatus.OutForDelivery, courier.Id, now);
        // Új megoldás:
        _packageService.SetPackageStatus(package, PackageStatus.OutForDelivery, courier.Id, now);
        _context.WarehouseLogs.Add(new WarehouseLog
        {
            Date = now, PackageCount = sizes.Count, UsedCapacity = usedCapacity, Capacity = _warehouseCapacity
        });

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok("A kiszállítás megkezdve.");
    }

    [Authorize(Roles = "Courier,Admin")]
    [HttpPut("{packageid}/completedelivery")]
    public async Task<IActionResult> CompleteDelivery(int packageid, [FromBody] PackageDTO dto)
    {
        if (dto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        if (!User.IsInRole("Admin") && User.FindFirst("id")?.Value != dto.CourierId.ToString()) return Forbid();
        if (dto.IsPaid != true) return BadRequest("A kézbesítés csak a szállítási díj átvétele után rögzíthető.");

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Régi megoldás a közös lekérő előtt:
        // var package = await _context.Packages.FindAsync(packageid);
        // var courier = await _context.Users.FindAsync(dto.CourierId);
        //
        // if (package == null) return NotFound("A csomag nem található.");
        //
        // if (courier == null) return NotFound("A futár nem található.");
        // Új megoldás:
        // Régi megoldás:
        // var result = await GetPackageAndCourier(packageid, dto.CourierId.Value);
        // if (result.Error != null) return result.Error;
        // Új megoldás:
        var result = await _packageService.GetPackageAndCourier(packageid, dto.CourierId.Value);
        if (result.Error != null) return NotFound(result.Error);

        var package = result.Package!;
        var courier = result.Courier!;

        if (courier.Role != UserRole.Courier) return BadRequest("A felhasználó nem futár.");

        var tasks = await _context.CourierTasks
            .Where(task => task.PackageId == packageid && task.Status != CourierTaskStatus.Completed).ToListAsync();

        if (tasks.Count == 0) return NotFound("Nincs aktív kiszállítási feladat.");

        if (tasks.Count != 1) return Conflict("A csomaghoz több aktív feladat tartozik.");

        var task = tasks[0];

        if (task.CourierId != courier.Id) return Forbid();

        if (task.TaskType != CourierTaskType.Delivery || task.Status != CourierTaskStatus.InProgress || !task.IsLocked ||
            package.CurrentStatus != PackageStatus.OutForDelivery || package.CourierId != courier.Id)
            return Conflict("Csak a futár megkezdett, zárolt kiszállítása zárható le.");

        if (package.ShippingCost == null) return Conflict("A csomag szállítási díja nem ismert. Előbb rendezni kell a díjadatot.");

        if (package.IsPaid) return Conflict("A csomag díjfizetését már rögzítették. Ellenőrizd az adatokat!");

        task.Status = CourierTaskStatus.Completed;
        package.IsPaid = true;

        package.CourierId = null;

        // Régi megoldás:
        // SetPackageStatus(package, PackageStatus.Delivered, courier.Id, DateTime.UtcNow);
        // Új megoldás:
        _packageService.SetPackageStatus(package, PackageStatus.Delivered, courier.Id, DateTime.UtcNow);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok("A csomagot kézbesítettük, a szállítási díj átvételét rögzítettük.");
    }

    private Task<List<CourierTaskDTO>> GetCourierTaskList(IQueryable<CourierTask> tasks)
    {
        return tasks.OrderBy(task => task.AssignmentTime).ThenBy(task => task.Id)
            .Select(task => new CourierTaskDTO
            {
                Id = task.Id, CourierId = task.CourierId, PackageId = task.PackageId,
                TaskType = task.TaskType, Status = task.Status, IsLocked = task.IsLocked,
                AssignmentTime = DateTime.SpecifyKind(task.AssignmentTime, DateTimeKind.Utc),
                Package = new PackageDTO
                {
                    Id = task.PackageId, TrackingNumber = task.Package.TrackingNumber,
                    SenderName = task.Package.SenderName, SenderAddress = task.Package.SenderAddress,
                    SenderPhoneNumber = task.Package.SenderPhoneNumber,
                    RecipientName = task.Package.RecipientName, RecipientAddress = task.Package.RecipientAddress,
                    RecipientPhoneNumber = task.Package.RecipientPhoneNumber,
                    Size = task.Package.Size, Weight = task.Package.Weight,
                    CurrentStatus = task.Package.CurrentStatus,
                    PickupDate = task.Package.PickupDate, PickupSlot = task.Package.PickupSlot,
                    ShippingCost = task.Package.ShippingCost, IsPaid = task.Package.IsPaid
                }
            }).ToListAsync();
    }
}
