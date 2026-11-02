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
using Microsoft.Data.SqlClient;

namespace Csomagkuldo.Controllers;

[Route("api/Package")]
[ApiController]
public class WarehouseController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PackageService _packageService;
    private readonly int _warehouseCapacity;

    public WarehouseController(AppDbContext context, PackageService packageService, IConfiguration configuration)
    {
        _context = context;
        _packageService = packageService;
        _warehouseCapacity = configuration.GetValue<int>("Warehouse:Capacity");
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpGet("unassignedPrebooked")]
    public async Task<IActionResult> GetUnassignedPrebookedPackages()
    {
        var packages = _context.Packages.AsNoTracking()
            .Where(p => p.CurrentStatus == PackageStatus.PreBooked && p.CourierId == null &&
                !p.CourierTasks.Any(task => task.Status != CourierTaskStatus.Completed));
        var result = await GetWarehousePackageList(packages);
        return Ok(result);
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpGet("warehouse")]
    public async Task<IActionResult> GetWarehousePackages()
    {
        var packages = _context.Packages.AsNoTracking()
            .Where(p => p.CurrentStatus == PackageStatus.AtWarehouse || p.CurrentStatus == PackageStatus.AssignedForDelivery);
        var result = await GetWarehousePackageList(packages);
        return Ok(result);
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpGet("unassignedWarehouse")]
    public async Task<IActionResult> GetUnassignedWarehousePackages()
    {
        var packages = _context.Packages.AsNoTracking()
            .Where(p => p.CurrentStatus == PackageStatus.AtWarehouse && p.CourierId == null &&
                !p.CourierTasks.Any(task => task.Status != CourierTaskStatus.Completed));
        var result = await GetWarehousePackageList(packages);
        return Ok(result);
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpGet("assigned")]
    [ProducesResponseType(typeof(List<PackageDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignedPackages()
    {
        var packages = _context.Packages.AsNoTracking()
            .Where(p => p.CourierId != null &&
                (p.CurrentStatus == PackageStatus.AssignedForPickup ||
                 p.CurrentStatus == PackageStatus.InTransitToWarehouse ||
                 p.CurrentStatus == PackageStatus.AssignedForDelivery ||
                 p.CurrentStatus == PackageStatus.OutForDelivery ||
                 p.CurrentStatus == PackageStatus.FailedDelivery));
        return Ok(await GetWarehousePackageList(packages));
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpPut("{packageid}/assignforpickup")]
    public async Task<IActionResult> AssignCourierToPackage(int packageid, [FromBody] PackageDTO courierdto)
    {
        if (courierdto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await _packageService.GetPackageAndCourier(packageid, courierdto.CourierId.Value);
        if (result.Error != null) return NotFound(result.Error);

        var package = result.Package!;
        var courier = result.Courier!;
        if (courier.Role != UserRole.Courier) return BadRequest("A kiválasztott felhasználó nem futár!");
        if (!courier.IsActive) return BadRequest("Inaktív futárhoz nem rendelhető új feladat!");

        var activeTask = await _context.CourierTasks.FirstOrDefaultAsync(task =>
            task.PackageId == packageid && task.Status != CourierTaskStatus.Completed);

        if (activeTask != null)
        {
            if (activeTask.IsLocked && !User.IsInRole("Admin"))
                return Conflict("A megkezdett feladat futárját a raktáros nem módosíthatja.");
            return Conflict("A csomaghoz már tartozik aktív futárfeladat.");
        }

        if (package.CurrentStatus != PackageStatus.PreBooked || package.CourierId != null)
            return Conflict("Csak kiosztatlan, előjegyzett csomag rendelhető felvételre.");

        string? capacityError = await CourierCapacityHelper.GetAssignmentError(_context, courier.Id, package.Size);

        if (capacityError != null) return Conflict(capacityError);

        DateTime now = DateTime.UtcNow;

        _packageService.AssignPackage(package, courier.Id, CourierTaskType.Pickup, now);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok("A futárt sikeresen hozzárendeltük a csomag felvételéhez.");
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpPut("{packageid}/reassigncourier")]
    public async Task<IActionResult> ReassignCourier(int packageid, [FromBody] PackageDTO dto)
    {
        if (dto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await _packageService.GetPackageAndCourier(packageid, dto.CourierId.Value);
        if (result.Error != null) return NotFound(result.Error);

        var package = result.Package!;
        var courier = result.Courier!;

        if (courier.Role != UserRole.Courier || !courier.IsActive)
            return BadRequest("Csak aktív futár választható!");

        var tasks = await _context.CourierTasks
            .Where(task => task.PackageId == packageid && task.Status != CourierTaskStatus.Completed).ToListAsync();

        if (tasks.Count == 0) return NotFound("Nincs átrendelhető feladat.");
        if (tasks.Count != 1) return Conflict("A csomaghoz több aktív feladat tartozik.");

        var task = tasks[0];

        if (task.IsLocked || task.Status != CourierTaskStatus.Assigned)
            return Conflict("A megkezdett feladat ezen a végponton már nem rendelhető át.");

        bool pickup = task.TaskType == CourierTaskType.Pickup && package.CurrentStatus == PackageStatus.AssignedForPickup;
        bool delivery = task.TaskType == CourierTaskType.Delivery && package.CurrentStatus == PackageStatus.AssignedForDelivery;

        if ((!pickup && !delivery) || package.CourierId != task.CourierId)
            return Conflict("A csomag és a futárfeladat adatai nem egyeznek.");

        if (task.CourierId == courier.Id) return Conflict("A csomag már ehhez a futárhoz tartozik.");

        string? capacityError = await CourierCapacityHelper.GetAssignmentError(_context, courier.Id, package.Size);

        if (capacityError != null) return Conflict(capacityError);

        DateTime now = DateTime.UtcNow;
        task.CourierId = courier.Id;
        task.AssignmentTime = now;
        package.CourierId = courier.Id;
        _packageService.SetPackageStatus(package, package.CurrentStatus, courier.Id, now);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok("A futárt sikeresen módosítottuk.");
    }

    [Authorize(Roles = "Admin,WarehouseOperator")]
    [HttpPut("{packageid}/receivetowarehouse")]
    public async Task<IActionResult> ReceiveToWarehouse(int packageid, [FromBody] PackageDTO dto)
    {
        if (dto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        if (_warehouseCapacity <= 0) return Problem("A raktár kapacitása nincs megfelelően beállítva.");
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await _packageService.GetPackageAndCourier(packageid, dto.CourierId.Value);
        if (result.Error != null) return NotFound(result.Error);

        var package = result.Package!;
        var courier = result.Courier!;

        var couriertask = await _context.CourierTasks.FirstOrDefaultAsync(task =>
            task.CourierId == dto.CourierId && task.PackageId == packageid &&
            task.TaskType == CourierTaskType.Pickup && task.Status == CourierTaskStatus.InProgress);

        if (couriertask == null) return NotFound("A folyamatban lévő felvételi feladat nem található!");

        if (package.CurrentStatus != PackageStatus.InTransitToWarehouse || package.CourierId != courier.Id)
            return Conflict("Csak az adott futár által felvett, raktárba tartó csomag vételezhető be.");

        var sizes = await _context.Packages.AsNoTracking()
            .Where(p => p.CurrentStatus == PackageStatus.AtWarehouse || p.CurrentStatus == PackageStatus.AssignedForDelivery)
            .Select(p => p.Size).ToListAsync();

        int usedCapacity = PackageSizeHelper.GetSpace(package.Size);

        if (usedCapacity == 0) return Conflict("A beérkező csomag mérete nem S, M vagy L.");

        int? warehouseSpace = PackageSizeHelper.GetTotalSpace(sizes);
        if (warehouseSpace == null) return Conflict("A raktárban ismeretlen méretű csomag található. Előbb javítani kell az adatát.");
        usedCapacity += warehouseSpace.Value;

        if (usedCapacity > _warehouseCapacity) return Conflict("Nincs elegendő hely a raktárban.");

        DateTime now = DateTime.UtcNow;
        couriertask.Status = CourierTaskStatus.Completed;
        couriertask.IsLocked = true;
        package.CourierId = null;

        _packageService.SetPackageStatus(package, PackageStatus.AtWarehouse, courier.Id, now);
        _context.WarehouseLogs.Add(new WarehouseLog
        {
            Date = now, PackageCount = sizes.Count + 1,
            UsedCapacity = usedCapacity, Capacity = _warehouseCapacity
        });

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok("Csomag a raktárba került!");
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpPut("{packageid}/assignfordelivery")]
    public async Task<IActionResult> Delivery(int packageid, [FromBody] PackageDTO dto)
    {
        if (dto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var result = await _packageService.GetPackageAndCourier(packageid, dto.CourierId.Value);
        if (result.Error != null) return NotFound(result.Error);

        var package = result.Package!;
        var courier = result.Courier!;
        if (courier.Role != UserRole.Courier) return BadRequest("A kiválasztott felhasználó nem futár!");
        if (!courier.IsActive) return BadRequest("Inaktív futárhoz nem rendelhető új feladat!");

        var activeTask = await _context.CourierTasks.FirstOrDefaultAsync(task =>
            task.PackageId == packageid && task.Status != CourierTaskStatus.Completed);
        if (activeTask != null)
        {
            if (activeTask.IsLocked && !User.IsInRole("Admin"))
                return Conflict("A megkezdett feladat futárját a raktáros nem módosíthatja.");
            return Conflict("A csomaghoz már tartozik aktív futárfeladat.");
        }
        if (package.CurrentStatus != PackageStatus.AtWarehouse || package.CourierId != null)
            return Conflict("Csak kiosztatlan, raktárban lévő csomag rendelhető kiszállításra.");
        string? capacityError = await CourierCapacityHelper.GetAssignmentError(_context, courier.Id, package.Size);
        if (capacityError != null) return Conflict(capacityError);

        DateTime now = DateTime.UtcNow;
        _packageService.AssignPackage(package, courier.Id, CourierTaskType.Delivery, now);

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok("A futárt sikeresen hozzárendeltük a csomag kiszállításához.");
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpPost("autoassignfordelivery")]
    [ProducesResponseType(typeof(DeliveryAssignmentResultDTO), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignDeliveriesAutomatically()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var packages = await _context.Packages
                    .Where(package => package.CurrentStatus == PackageStatus.AtWarehouse && package.CourierId == null &&
                        !package.CourierTasks.Any(task => task.Status != CourierTaskStatus.Completed))
                    .OrderBy(package => package.CreatedAt).ThenBy(package => package.Id).ToListAsync();
                var cars = await _context.CourierCars.AsNoTracking()
                    .Where(car => car.Courier != null && car.Courier.IsActive && car.Courier.Role == UserRole.Courier)
                    .OrderBy(car => car.Id).ToListAsync();
                var result = new DeliveryAssignmentResultDTO();

                foreach (var car in cars)
                {
                    int courierId = car.CourierId!.Value;
                    int? usedSpace = await CourierCapacityHelper.GetUsedSpace(_context, courierId);

                    if (usedSpace == null) continue;
                    int freeSpace = car.Capacity - usedSpace.Value;

                    if (freeSpace <= 0) continue;

                    foreach (var package in packages)
                    {
                        if (package.CourierId != null) continue;
                        int requiredSpace = PackageSizeHelper.GetSpace(package.Size);
                        if (requiredSpace == 0 || requiredSpace > freeSpace) continue;

                        DateTime now = DateTime.UtcNow;
                        _packageService.AssignPackage(package, courierId, CourierTaskType.Delivery, now);

                        await _context.SaveChangesAsync();

                        freeSpace -= requiredSpace;
                        result.Assigned.Add(new PackageDTO
                        {
                            Id = package.Id, TrackingNumber = package.TrackingNumber, CourierId = courierId
                        });
                        if (freeSpace == 0) break;
                    }
                }

                foreach (var package in packages.Where(package => package.CourierId == null))
                {
                    result.Unassigned.Add(new PackageDTO
                    {
                        Id = package.Id, TrackingNumber = package.TrackingNumber,
                        Reason = PackageSizeHelper.GetSpace(package.Size) == 0
                            ? "A csomag mérete nem S, M vagy L."
                            : "Nincs megfelelő autóval és elegendő szabad hellyel rendelkező aktív futár."
                    });
                }

                await transaction.CommitAsync();

                return Ok(result);
            }
            catch (Exception exception) when (exception.GetBaseException() is SqlException sql && sql.Number == 1205)
            {
                _context.ChangeTracker.Clear();
            }
        }
        return Conflict("A kiosztás másik művelettel ütközött. Frissítsd a listát és próbáld újra!");
    }

    private Task<List<PackageDTO>> GetWarehousePackageList(IQueryable<Package> packages)
    {
        return packages.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => new PackageDTO
            {
                Id = p.Id,
                TrackingNumber = p.TrackingNumber,
                SenderName = p.SenderName,
                SenderAddress = p.SenderAddress,
                RecipientName = p.RecipientName,
                RecipientAddress = p.RecipientAddress,
                Weight = p.Weight,
                Size = p.Size,
                CurrentStatus = p.CurrentStatus,
                CourierId = p.CourierId,
                CanReassign = p.CourierId != null &&
                    p.CourierTasks.Count(task => task.Status != CourierTaskStatus.Completed) == 1 &&
                    p.CourierTasks.Any(task => task.CourierId == p.CourierId &&
                        task.Status == CourierTaskStatus.Assigned && !task.IsLocked &&
                        ((task.TaskType == CourierTaskType.Pickup && p.CurrentStatus == PackageStatus.AssignedForPickup) ||
                         (task.TaskType == CourierTaskType.Delivery && p.CurrentStatus == PackageStatus.AssignedForDelivery))),
                CanReceive = p.CurrentStatus == PackageStatus.InTransitToWarehouse && p.CourierId != null &&
                    p.CourierTasks.Count(task => task.Status != CourierTaskStatus.Completed) == 1 &&
                    p.CourierTasks.Any(task => task.CourierId == p.CourierId &&
                        task.TaskType == CourierTaskType.Pickup && task.Status == CourierTaskStatus.InProgress),
                PickupDate = p.PickupDate,
                PickupSlot = p.PickupSlot,
                CreatedAt = DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc)
            }).ToListAsync();
    }
}
