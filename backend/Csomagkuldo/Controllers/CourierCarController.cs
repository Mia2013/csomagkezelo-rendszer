using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Csomagkuldo.Enums;
using Csomagkuldo.Helpers;
using Csomagkuldo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Csomagkuldo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CourierCarController : ControllerBase
{
    private readonly AppDbContext _context;

    public CourierCarController(AppDbContext context)
    {
        _context = context;
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cars = await _context.CourierCars.AsNoTracking().OrderBy(car => car.Id).ToListAsync();
        return Ok(cars.Select(ToDto));
    }

    [Authorize(Roles = "WarehouseOperator,Admin")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var car = await _context.CourierCars.AsNoTracking().FirstOrDefaultAsync(car => car.Id == id);
        if (car == null) return NotFound("Az autó nem található.");
        return Ok(ToDto(car));
    }

    [Authorize(Roles = "Courier,WarehouseOperator,Admin")]
    [HttpGet("courier/{courierId}/capacity")]
    public async Task<IActionResult> GetCapacity(int courierId)
    {
        if (User.IsInRole("Courier") && User.FindFirst("id")?.Value != courierId.ToString()) return Forbid();
        var courier = await _context.Users.FindAsync(courierId);
        if (courier == null) return NotFound("A futár nem található.");
        if (courier.Role != UserRole.Courier) return BadRequest("A felhasználó nem futár.");
        var car = await _context.CourierCars.AsNoTracking().FirstOrDefaultAsync(car => car.CourierId == courierId);
        if (car == null) return NotFound("A futárhoz nincs autó rendelve.");
        int? used = await CourierCapacityHelper.GetUsedSpace(_context, courierId);
        if (used == null) return Conflict("Ismeretlen csomagméret miatt a helyfoglalás nem számítható ki.");
        if (used.Value > car.Capacity) return Conflict("A kiosztott csomagok meghaladják az autó kapacitását.");
        return Ok(new CourierCarDTO
        {
            CourierId = courierId, Id = car.Id, IsActive = courier.IsActive,
            Capacity = car.Capacity, UsedCapacity = used.Value, FreeCapacity = car.Capacity - used.Value
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CourierCarDTO dto)
    {
        if (!ValidateCarData(dto)) return ValidationProblem(ModelState);
        string plate = dto.LicensePlate!.Trim().ToUpperInvariant();
        if (await _context.CourierCars.AnyAsync(car => car.LicensePlate == plate))
            return Conflict("Ez a rendszám már szerepel a rendszerben.");
        var car = new CourierCar();
        SetCarData(car, dto);
        _context.CourierCars.Add(car);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = car.Id }, ToDto(car));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CourierCarDTO dto)
    {
        if (!ValidateCarData(dto)) return ValidationProblem(ModelState);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var car = await _context.CourierCars.FindAsync(id);
        if (car == null) return NotFound("Az autó nem található.");
        string plate = dto.LicensePlate!.Trim().ToUpperInvariant();
        if (await _context.CourierCars.AnyAsync(other => other.Id != id && other.LicensePlate == plate))
            return Conflict("Ez a rendszám már szerepel a rendszerben.");
        if (car.CourierId != null)
        {
            int? used = await CourierCapacityHelper.GetUsedSpace(_context, car.CourierId.Value);
            if (used == null || used.Value > dto.Capacity)
                return Conflict("Az új kapacitás nem elegendő a futár meglévő feladataihoz.");
        }
        SetCarData(car, dto);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ToDto(car));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}/assigncourier")]
    public async Task<IActionResult> AssignCourier(int id, CourierCarDTO dto)
    {
        if (dto.CourierId == null) return BadRequest("Adj meg egy érvényes futárazonosítót!");
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var car = await _context.CourierCars.FindAsync(id);
        var courier = await _context.Users.FindAsync(dto.CourierId);
        if (car == null) return NotFound("Az autó nem található.");
        if (courier == null) return NotFound("A futár nem található.");
        if (courier.Role != UserRole.Courier || !courier.IsActive)
            return BadRequest("Csak aktív futárhoz rendelhető autó.");
        if (car.CourierId == courier.Id) return Ok(ToDto(car));
        if (car.CourierId != null && await HasActiveTasks(car.CourierId.Value))
            return Conflict("Az autó jelenlegi futárjának még vannak aktív feladatai.");

        int? used = await CourierCapacityHelper.GetUsedSpace(_context, courier.Id);
        if (used == null || used.Value > car.Capacity)
            return Conflict("Az autó nem elegendő a futár meglévő feladataihoz.");
        var oldCar = await _context.CourierCars.FirstOrDefaultAsync(other => other.CourierId == courier.Id);
        if (oldCar != null)
        {
            oldCar.CourierId = null;
            // Az egyedi futárkapcsolatot előbb felszabadítjuk, ugyanabban a tranzakcióban.
            await _context.SaveChangesAsync();
        }
        car.CourierId = courier.Id;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ToDto(car));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}/unassigncourier")]
    public async Task<IActionResult> UnassignCourier(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var car = await _context.CourierCars.FindAsync(id);
        if (car == null) return NotFound("Az autó nem található.");
        if (car.CourierId != null && await HasActiveTasks(car.CourierId.Value))
            return Conflict("Aktív feladatok mellett csak megfelelő másik autóra cserélhető az autó.");
        car.CourierId = null;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(ToDto(car));
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var car = await _context.CourierCars.FindAsync(id);
        if (car == null) return NotFound("Az autó nem található.");
        if (car.CourierId != null && await HasActiveTasks(car.CourierId.Value))
            return Conflict("Aktív feladatokat kezelő futár autója nem törölhető.");
        _context.CourierCars.Remove(car);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    private Task<bool> HasActiveTasks(int courierId)
    {
        return _context.CourierTasks.AnyAsync(task => task.CourierId == courierId && task.Status != CourierTaskStatus.Completed);
    }

    private bool ValidateCarData(CourierCarDTO dto)
    {
        if (dto.Capacity == null) ModelState.AddModelError(nameof(dto.Capacity), "Add meg az autó kapacitását!");
        if (string.IsNullOrWhiteSpace(dto.LicensePlate)) ModelState.AddModelError(nameof(dto.LicensePlate), "Add meg a rendszámot!");
        if (string.IsNullOrWhiteSpace(dto.Manufacturer)) ModelState.AddModelError(nameof(dto.Manufacturer), "Add meg a gyártót!");
        if (string.IsNullOrWhiteSpace(dto.Model)) ModelState.AddModelError(nameof(dto.Model), "Add meg a modellt!");
        if (dto.Year == null) ModelState.AddModelError(nameof(dto.Year), "Add meg az évjáratot!");
        return ModelState.IsValid;
    }

    private static void SetCarData(CourierCar car, CourierCarDTO dto)
    {
        car.Capacity = dto.Capacity!.Value;
        car.LicensePlate = dto.LicensePlate!.Trim().ToUpperInvariant();
        car.Manufacturer = dto.Manufacturer!.Trim();
        car.Model = dto.Model!.Trim();
        car.Year = dto.Year!.Value;
    }

    private static CourierCarDTO ToDto(CourierCar car)
    {
        return new CourierCarDTO
        {
            Id = car.Id, Capacity = car.Capacity, CourierId = car.CourierId,
            LicensePlate = car.LicensePlate, Manufacturer = car.Manufacturer, Model = car.Model, Year = car.Year
        };
    }
}
