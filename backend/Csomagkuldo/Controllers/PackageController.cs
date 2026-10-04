using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Csomagkuldo.Enums;
using Csomagkuldo.Helpers;
using Csomagkuldo.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PackageController : ControllerBase
{
    private readonly AppDbContext _context;

    public PackageController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create(CreatePackageDTO dto)
    {
        DateTime now = DateTime.UtcNow;
        if (!BookingRules.IsValidPickupDate(dto.PickupDate!.Value, now))
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

        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(),
            SenderId = senderId,
            SenderName = dto.SenderName.Trim(),
            SenderAddress = dto.SenderAddress.Trim(),
            SenderPhoneNumber = dto.SenderPhoneNumber.Trim(),
            RecipientName = dto.RecipientName.Trim(),
            RecipientAddress = dto.RecipientAddress.Trim(),
            RecipientPhoneNumber = dto.RecipientPhoneNumber.Trim(),
            RecipientEmail = dto.RecipientEmail.Trim(),
            Value = dto.Value!.Value,
            Weight = dto.Weight!.Value,
            Size = dto.Size,
            PickupDate = dto.PickupDate.Value.ToDateTime(TimeOnly.MinValue),
            PickupSlot = dto.PickupSlot,
            CurrentStatus = PackageStatus.PreBooked,
            CreatedAt = now
        };

        package.StatusHistory.Add(new PackageStatusHistory
        {
            Status = PackageStatus.PreBooked,
            ChangedAt = now
        });

        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        var result = new PackageCreatedDTO
        {
            TrackingNumber = package.TrackingNumber,
            Status = package.CurrentStatus,
            CreatedAt = package.CreatedAt
        };
        return CreatedAtAction(nameof(Track), new { trackingNumber = package.TrackingNumber }, result);
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

        var lastStatus = await _context.PackageStatuses.AsNoTracking()
            .Where(item => item.PackageId == package.Id)
            .OrderByDescending(item => item.ChangedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync();
        if (lastStatus == null)
        {
            return Problem("A csomag státuszelőzménye hiányzik.");
        }

        return Ok(new PackageTrackingDTO
        {
            TrackingNumber = package.TrackingNumber,
            Status = lastStatus.Status,
            // SQL Server a DateTime Kind értékét nem tárolja; az eseményidők UTC-ben vannak.
            ChangedAt = DateTime.SpecifyKind(lastStatus.ChangedAt, DateTimeKind.Utc)
        });
    }
}
