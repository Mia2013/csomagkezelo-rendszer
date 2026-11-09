using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Csomagkuldo.Enums;
using Csomagkuldo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class WareHouseController : Controller
    {
        private readonly AppDbContext _context;

        public WareHouseController(AppDbContext context)
        {
            _context = context;
        }
        [Authorize(Roles = "WarehouseOperatior,Admin")]
        [HttpGet("unassignedPrebooked")]
        public async Task<IActionResult> GetUnassignedPrebookedPackages()
        {
            var result = await _context.Packages.Where(p => p.CurrentStatus == PackageStatus.PreBooked && p.CourierId == null).ToListAsync();
            if (result == null)
            {
                return NotFound("Nincs felvételre váró csomag!");
            }
            return Ok(result);

        }
        //CSOMAG FUTÁRHOZ RENDELÉSE
        [Authorize(Roles = "WarehouseOperator,Admin")]
        [HttpPut("{packageid}/assignforpickup")]
        public async Task<IActionResult> AssignCourierToPackage(int packageid, [FromBody] AssignCourierRequestDTO courierdto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == courierdto.CourierId);
            if (package == null || courier == null)
            {
                return BadRequest("A hozzárendelés nem hajtható végre!");
            }
            await _context.CourierTasks.AddAsync(new CourierTask
            {
                PackageId = packageid,
                Status = CourierTaskStatus.Assigned,
                CourierId = courier.Id,
                AssignmentTime = DateTime.UtcNow

            });
            await _context.PackageStatuses.AddAsync(new PackageStatusHistory
            {
                PackageId = packageid,
                Status = PackageStatus.AssignedForPickup,
                ChangedAt = DateTime.UtcNow
            });

            package.CurrentStatus = PackageStatus.AssignedForPickup;
            await _context.SaveChangesAsync();
            return Ok("Futár sikeresen  a csomaghoz lett rendelve!");
        }
        //CSOMAG MEGÉRKEZIK A RAKTÁRBA
        [Authorize(Roles = "Courier,Admin,WarehouseOperator")]
        [HttpPut("{packageid}/receivetowarehouse")]
        public async Task<IActionResult> ReceiveToWarehouse(int packageid, [FromBody] CourierDTO dto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == dto.CourierId);
            if (package == null)
            {
                return NotFound("A csomag nem található!");
            }
            if (courier == null)
            {
                return NotFound("A futár nem található!");
            }
            var couriertask = await _context.CourierTasks.FirstOrDefaultAsync(c => c.Id == dto.CourierId && c.PackageId == packageid);

            couriertask.Status = CourierTaskStatus.Completed;

            package.CurrentStatus = PackageStatus.AtWarehouse;
            await _context.PackageStatuses.AddAsync(new PackageStatusHistory
            {
                PackageId = packageid,
                Status = PackageStatus.AtWarehouse,
                ChangedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return Ok("Csomag a raktárba került!");
        }
        //FUTÁR HOZZÁRENDELÉSE A CSOMAG KISZÁLLÍTÁSÁHOZ
        [Authorize(Roles = "WarehouseOperator,Admin")]
        [HttpPut("{packageid}/assignfordelivery")]
        public async Task<IActionResult> Delivery(int packageid, [FromBody] AssignCourierRequestDTO dto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == dto.CourierId);
            if (package == null || courier == null)
            {
                return BadRequest("Hozzárendelés nem hajtható végre!");

            }
            if (package == null || courier == null)
            {
                return BadRequest("A hozzárendelés nem hajtható végre!");
            }

            await _context.CourierTasks.AddAsync(new CourierTask
            {
                PackageId = packageid,
                Status = CourierTaskStatus.Assigned,
                CourierId = courier.Id,
                AssignmentTime = DateTime.UtcNow

            });
            await _context.PackageStatuses.AddAsync(new PackageStatusHistory
            {
                PackageId = packageid,
                Status = PackageStatus.AssignedForPickup,
                ChangedAt = DateTime.UtcNow
            });

            package.CurrentStatus = PackageStatus.AssignedForPickup;
            await _context.SaveChangesAsync();
            return Ok("Futár sikeresen  a csomaghoz lett rendelve!");

        }
    }
}
