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
using System.Security.Claims;

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

    //CSOMAG ELŐJEGYZÉS LÉTREHOZÁSA
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
            TrackingNumber = Guid.NewGuid().ToString("N")[..10].ToUpper(),
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
        string normalizedNumber = trackingNumber?.Trim() ?? string.Empty;
        if (normalizedNumber.Length == 0)
        {
            return BadRequest("Adj meg egy érvényes csomagazonosítót!");
        }

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
        return Ok(new PackageTrackingDTO
        {
            TrackingNumber = package.TrackingNumber,
            Status = lastStatus.Status,
            ChangedAt = lastStatus.ChangedAt,
            History = history
        });
    }
    
    // REGISZTRÁLT FELHASZNÁLÓ CSOMAG MÓDOSÍTÁSA
    [Authorize]
    [HttpPut("update/{packageId}")]
    public async Task<IActionResult> UpdatePackage(int packageId, [FromBody] UpdatePackageDTO dto)
    {
        var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageId);
        if (package == null)
        {
            return NotFound("A csomag nem található!");
        }
        var UserEmail = User.Identity?.Name;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email==UserEmail);
        if (user==null||package.SenderId!=user.Id)
        {
            return BadRequest("Csak a saját előjegyzett csomagodat módosíthatod!");
        }

  
        if (package.CurrentStatus != PackageStatus.PreBooked)
        {
            return BadRequest("A csomag már nem módosítható!");
        }

        if (DateTime.UtcNow.Date >= package.PickupDate.Date)
        {
            return BadRequest("A csomag adatait legkésőbb a felvétel előtti napig lehet módoítani!");
        }
        if (!string.IsNullOrWhiteSpace(dto.PickupSlot))
        {
            package.PickupSlot = dto.PickupSlot;
        }

        if (!string.IsNullOrWhiteSpace(dto.RecipientAddress))
        {
            package.RecipientAddress = dto.RecipientAddress;
        }

        if (dto.Size.HasValue)
        {
            package.Size = dto.Size.Value;
        }

        await _context.SaveChangesAsync();
        return Ok("A csomag adatai sikeresen frissítve!");
    }

    //REGISZTRÁLT FELHASZNÁLÓ CSOMAGJAINAK LISTÁZÁSA
    [Authorize]
    [HttpGet("getAllByUser")]
    public async Task<IActionResult> GetAllByUser()
    {
        int userId = int.Parse(User.FindFirst("id")!.Value);
        var user = await _context.Users.FindAsync(userId);
        string? email = user?.Email;

        var packages = await _context.Packages
            .Where(p => p.SenderId == userId || (email != null && p.RecipientEmail == email))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PackageListItemDTO
            {
                Id = p.Id,
                TrackingNumber = p.TrackingNumber,
                SenderName = p.SenderName,
                SenderAddress = p.SenderAddress,
                RecipientName = p.RecipientName,
                RecipientAddress = p.RecipientAddress,
                Size = p.Size,
                PickupDate = p.PickupDate,
                PickupSlot = p.PickupSlot,
                CurrentStatus = p.CurrentStatus,
                CreatedAt = p.CreatedAt,
                IsSender = p.SenderId == userId
            })
            .ToListAsync();

        return Ok(packages);
    }

}