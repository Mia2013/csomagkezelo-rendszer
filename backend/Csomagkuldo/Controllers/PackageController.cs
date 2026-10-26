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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;

namespace Csomagkuldo.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PackageController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PackageService _packageService;

    // Régi megoldás:
    // public PackageController(AppDbContext context, IConfiguration configuration)
    // {
    //     _context = context;
    //     _warehouseCapacity = configuration.GetValue<int>("Warehouse:Capacity");
    // }
    // Új megoldás:
    public PackageController(AppDbContext context, PackageService packageService)
    {
        _context = context;
        _packageService = packageService;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create(PackageDTO dto)
    {
        if (!ValidatePackageCreation(dto)) return ValidationProblem(ModelState);
        DateTime now = DateTime.UtcNow;
        if (!BookingRules.IsValidPickupDate(DateOnly.FromDateTime(dto.PickupDate!.Value), now))
        {
            ModelState.AddModelError(nameof(dto.PickupDate),
                "A felvétel legkorábban a következő budapesti naptári napra kérhető.");
            return ValidationProblem(ModelState);
        }

        int? senderId = null;
        // A hibás tokennel érkező kérést nem mentjük el vendégfeladásként.
        if (Request.Headers.ContainsKey("Authorization"))
        {
            var authentication = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (!authentication.Succeeded ||
                !int.TryParse(authentication.Principal?.FindFirst("id")?.Value, out int userId) ||
                !await _context.Users.AnyAsync(user => user.Id == userId))
            {
                return Unauthorized("A bejelentkezés lejárt vagy érvénytelen. Jelentkezz be újra!");
            }
            senderId = userId;
        }

        // A címzett a már létező felhasználó e-mailje és címe alapján azonosítható.
        string recipientEmail = dto.RecipientEmail!.Trim().ToUpperInvariant();
        string recipientAddress = dto.RecipientAddress!.Trim().ToUpperInvariant();
        var recipientIds = await _context.Users
            .Where(user => user.Email != null && user.Email.Trim().ToUpper() == recipientEmail &&
                user.Address.Trim().ToUpper() == recipientAddress)
            .Select(user => user.Id).Take(2).ToListAsync();
        if (recipientIds.Count != 1)
            return BadRequest("A címzett e-mail-címével és címével nem található egyértelmű felhasználó. Ellenőrizd az adatokat; a címzettnek már regisztráltnak kell lennie.");

        // Ütközés után a teljes előjegyzést újraszámoljuk, legfeljebb háromszor.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var fee = await _context.ShippingFees.AsNoTracking().SingleOrDefaultAsync(fee => fee.MaxSize == dto.Size);
                if (fee == null) return Problem("A csomag méretéhez nincs beállítva szállítási díj.");
                int shippingCost = senderId != null ? fee.RegisteredPrice : fee.UnregisteredPrice;
                var package = new Package
                {
                    TrackingNumber = Guid.NewGuid().ToString(),
                    SenderId = senderId,
                    RecipientId = recipientIds[0],
                    SenderName = dto.SenderName!.Trim(),
                    SenderAddress = dto.SenderAddress!.Trim(),
                    SenderPhoneNumber = dto.SenderPhoneNumber!.Trim(),
                    RecipientName = dto.RecipientName!.Trim(),
                    RecipientAddress = dto.RecipientAddress!.Trim(),
                    RecipientPhoneNumber = dto.RecipientPhoneNumber!.Trim(),
                    RecipientEmail = dto.RecipientEmail!.Trim(),
                    Value = dto.Value!.Value,
                    Weight = dto.Weight!.Value,
                    Size = dto.Size!,
                    PickupDate = dto.PickupDate.Value.Date,
                    PickupSlot = dto.PickupSlot,
                    CurrentStatus = PackageStatus.PreBooked,
                    CreatedAt = now,
                    IsPaid = false,
                    ShippingFeeId = fee.Id,
                    ShippingCost = shippingCost
                };

                package.StatusHistory.Add(new PackageStatusHistory
                {
                    Status = PackageStatus.PreBooked,
                    ChangedAt = now
                });
                _context.Packages.Add(package);

                int? courierId = await FindAvailableCourier(package.Size);
                if (courierId != null &&
                    await CourierCapacityHelper.GetAssignmentError(_context, courierId.Value, package.Size) == null)
                {
                    DateTime assignedAt = DateTime.UtcNow;
                    // Régi megoldás:
                    // package.CourierId = courierId;
                    // package.CourierTasks.Add(new CourierTask
                    // {
                    //     CourierId = courierId.Value,
                    //     TaskType = CourierTaskType.Pickup,
                    //     Status = CourierTaskStatus.Assigned,
                    //     AssignmentTime = assignedAt
                    // });
                    // SetPackageStatus(package, PackageStatus.AssignedForPickup, courierId.Value, assignedAt);
                    // Új megoldás:
                    // Régi megoldás:
                    // AssignPackage(package, courierId.Value, CourierTaskType.Pickup, assignedAt);
                    // Új megoldás:
                    _packageService.AssignPackage(package, courierId.Value, CourierTaskType.Pickup, assignedAt);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return CreatedAtAction(nameof(Track), new { trackingNumber = package.TrackingNumber }, new PackageDTO
                {
                    TrackingNumber = package.TrackingNumber,
                    CurrentStatus = package.CurrentStatus,
                    CreatedAt = package.CreatedAt,
                    ShippingCost = shippingCost,
                    IsPaid = false
                });
            }
            catch (Exception exception) when (exception.GetBaseException() is SqlException sql && sql.Number == 1205)
            {
                // A visszagörgetett próbálkozás objektumait nem használjuk újra.
                _context.ChangeTracker.Clear();
            }
        }
        return Conflict("Az előjegyzés másik kiosztással ütközött. Próbáld újra!");
    }

    [AllowAnonymous]
    [HttpGet("track")]
    public async Task<IActionResult> Track([FromQuery] string trackingNumber)
    {
        if (!Guid.TryParse(trackingNumber?.Trim(), out Guid number))
        {
            return BadRequest("Adj meg egy érvényes csomagazonosítót!");
        }

        string normalizedNumber = number.ToString();
        var package = await _context.Packages.AsNoTracking()
            .Where(item => item.TrackingNumber == normalizedNumber)
            .Select(item => new { item.Id, item.TrackingNumber })
            .FirstOrDefaultAsync();
        if (package == null)
        {
            return NotFound("Ezzel az azonosítóval nem található csomag.");
        }

        var history = await _context.PackageStatuses.AsNoTracking()
            .Where(item => item.PackageId == package.Id)
            .OrderBy(item => item.ChangedAt)
            .ThenBy(item => item.Id)
            .Select(item => new PackageStatusDTO
            {
                Status = item.Status,
                // SQL Server a DateTime Kind értékét nem tárolja; az eseményidők UTC-ben vannak.
                ChangedAt = DateTime.SpecifyKind(item.ChangedAt, DateTimeKind.Utc)
            })
            .ToListAsync();
        if (history.Count == 0)
        {
            return Problem("A csomag státuszelőzménye hiányzik.");
        }

        var lastStatus = history.Last();
        return Ok(new PackageDTO
        {
            TrackingNumber = package.TrackingNumber,
            CurrentStatus = lastStatus.Status,
            ChangedAt = lastStatus.ChangedAt,
            History = history
        });
    }

    [Authorize]
    [HttpGet("mine/sent")]
    public async Task<IActionResult> GetMySentPackages([FromQuery] bool history = false)
    {
        if (!int.TryParse(User.FindFirst("id")?.Value, out int userId) || userId <= 0 ||
            !await _context.Users.AnyAsync(user => user.Id == userId))
            return Unauthorized("A bejelentkezés érvénytelen. Jelentkezz be újra!");

        var packages = _context.Packages.AsNoTracking().Where(package => package.SenderId == userId);
        var result = await GetMyPackageList(packages, history);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("mine/received")]
    public async Task<IActionResult> GetMyReceivedPackages([FromQuery] bool history = false)
    {
        if (!int.TryParse(User.FindFirst("id")?.Value, out int userId) || userId <= 0 ||
            !await _context.Users.AnyAsync(user => user.Id == userId))
            return Unauthorized("A bejelentkezés érvénytelen. Jelentkezz be újra!");

        var packages = _context.Packages.AsNoTracking().Where(package => package.RecipientId == userId);
        var result = await GetMyPackageList(packages, history);
        return Ok(result);
    }

    private async Task<int?> FindAvailableCourier(string size)
    {
        int requiredSpace = PackageSizeHelper.GetSpace(size);

        var cars = await _context.CourierCars.AsNoTracking()
            .Where(car => car.Courier != null && car.Courier.IsActive && car.Courier.Role == UserRole.Courier)
            .OrderBy(car => car.CourierId).ToListAsync();

        int? selectedCourierId = null;
        int mostFreeSpace = -1;

        foreach (var car in cars)
        {
            int? usedSpace = await CourierCapacityHelper.GetUsedSpace(_context, car.CourierId!.Value);

            if (usedSpace == null) continue;

            int freeSpace = car.Capacity - usedSpace.Value;

            if (freeSpace >= requiredSpace && freeSpace > mostFreeSpace)
            {
                mostFreeSpace = freeSpace;
                selectedCourierId = car.CourierId;
            }
        }
        return selectedCourierId;
    }

    private Task<List<PackageDTO>> GetMyPackageList(IQueryable<Package> packages, bool history)
    {
        // A meghiúsult kézbesítés még folyamatban lévő csomagügy.
        if (history) packages = packages.Where(package => package.CurrentStatus == PackageStatus.Delivered);
        else packages = packages.Where(package => package.CurrentStatus != PackageStatus.Delivered);

        return packages.OrderByDescending(package => package.CreatedAt).ThenByDescending(package => package.Id)
            .Select(package => new PackageDTO
            {
                Id = package.Id,
                TrackingNumber = package.TrackingNumber,
                SenderName = package.SenderName,
                SenderAddress = package.SenderAddress,
                RecipientName = package.RecipientName,
                RecipientAddress = package.RecipientAddress,
                Size = package.Size,
                Weight = package.Weight,
                ShippingCost = package.ShippingCost,
                IsPaid = package.IsPaid,
                CurrentStatus = package.CurrentStatus,
                PickupDate = package.PickupDate,
                PickupSlot = package.PickupSlot,
                CreatedAt = DateTime.SpecifyKind(package.CreatedAt, DateTimeKind.Utc)
            }).ToListAsync();
    }

    private bool ValidatePackageCreation(PackageDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SenderName)) ModelState.AddModelError(nameof(dto.SenderName), "Add meg a feladó nevét!");
        if (string.IsNullOrWhiteSpace(dto.SenderAddress)) ModelState.AddModelError(nameof(dto.SenderAddress), "Add meg a feladó címét!");
        if (string.IsNullOrWhiteSpace(dto.SenderPhoneNumber)) ModelState.AddModelError(nameof(dto.SenderPhoneNumber), "Add meg a feladó telefonszámát!");
        if (string.IsNullOrWhiteSpace(dto.RecipientName)) ModelState.AddModelError(nameof(dto.RecipientName), "Add meg a címzett nevét!");
        if (string.IsNullOrWhiteSpace(dto.RecipientAddress)) ModelState.AddModelError(nameof(dto.RecipientAddress), "Add meg a címzett címét!");
        if (string.IsNullOrWhiteSpace(dto.RecipientPhoneNumber)) ModelState.AddModelError(nameof(dto.RecipientPhoneNumber), "Add meg a címzett telefonszámát!");
        if (string.IsNullOrWhiteSpace(dto.RecipientEmail)) ModelState.AddModelError(nameof(dto.RecipientEmail), "Add meg a címzett e-mail-címét!");
        if (dto.Value == null) ModelState.AddModelError(nameof(dto.Value), "Add meg a csomag értékét!");
        if (dto.Weight == null) ModelState.AddModelError(nameof(dto.Weight), "Add meg a csomag tömegét!");
        if (string.IsNullOrWhiteSpace(dto.Size)) ModelState.AddModelError(nameof(dto.Size), "Add meg a csomag méretét!");
        if (dto.PickupDate == null) ModelState.AddModelError(nameof(dto.PickupDate), "Add meg a felvétel napját!");
        if (string.IsNullOrWhiteSpace(dto.PickupSlot)) ModelState.AddModelError(nameof(dto.PickupSlot), "Add meg a felvételi idősávot!");
        return ModelState.IsValid;
    }
}
