using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Csomagkuldo.Enums;
using Csomagkuldo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class CourierTaskController : Controller
    {
        private readonly AppDbContext _context;

        public CourierTaskController(AppDbContext context)
        {
            _context = context;
        }
        //CSOMAGOT FELVESZI A FELADOTT CSOMAGOT
        [Authorize(Roles = "Courier,Admin")]
        [HttpPut("{packageid}/pickuppackage")]
        public async Task<IActionResult> Pickup(int packageid, [FromBody] CourierDTO dto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == dto.CourierId);
            if (package == null)
            {
                return NotFound("Csomag nem található");
            }
            if (courier == null)
            {
                return NotFound("Futár nem található");
            }
            
            var couriertask = await _context.CourierTasks.FirstOrDefaultAsync(ct => ct.PackageId == packageid && ct.CourierId == dto.CourierId&&ct.Status == CourierTaskStatus.Assigned);
           
            if (couriertask == null)
            {
                return NotFound("A futárhoz nem tartozik ilyen feladat.");
            }
           
            var couriercar=await _context.CourierCars.FirstOrDefaultAsync(c=>c.CourierId==dto.CourierId);

            if (couriercar == null)
            {
                return BadRequest("A futárhoz nincs autó rendelve.");
            }

            couriertask.Status = CourierTaskStatus.InProgress;
            package.CurrentStatus = PackageStatus.InTransitToWarehouse;

            await _context.PackageStatuses.AddAsync(new PackageStatusHistory
            {
                Package = package,
                Status = PackageStatus.InTransitToWarehouse,
                ChangedAt = DateTime.UtcNow
            });
           
            await _context.SaveChangesAsync();
            return Ok("Csomag sikeresen felvéve!");
        }
        //FUTÁR MEGTEKINTHETI A FELVÉTELI FELADATAIT
        [Authorize(Roles = "Courier,Admin")]
        [HttpGet("{courierid}/pickuptasks")]
        public async Task<IActionResult> GetCourirerPickUpTasks(int courierId)
        {
            var courier = await _context.Users.FindAsync(courierId);
            if (courier == null)
            {
                return NotFound("Futár nem található!");
            }
            var couriertask = await _context.CourierTasks.FirstOrDefaultAsync(ct => ct.CourierId == courierId);

            var result = await _context.CourierTasks.Where(ct => ct.CourierId == courierId && ct.Status == CourierTaskStatus.Assigned && ct.Package.CurrentStatus == PackageStatus.AssignedForPickup).ToArrayAsync();

            return Ok(result switch
            {
                null => "Jelenleg nincs kijelölt feladat!",
                _ => "A feladatok sikeresen lekérve!"
            });
        }

        //FUTÁR MEGTEKINTHETI AZ ÖSSZES KISZÁLLÍTÁSI FELADATÁT
        [Authorize(Roles = "Courier,Admin")]
        [HttpGet("{courierid}/deliverytasks")]
        public async Task<IActionResult> GetCourirerDeiveryTasks(int courierId)
        {
            var courier = await _context.Users.FindAsync(courierId);
            if (courier == null)
            {
                return NotFound("Futár nem található!");
            }

            var result = await _context.CourierTasks.Where(ct => ct.CourierId == courierId && ct.Status == CourierTaskStatus.Assigned && ct.Package.CurrentStatus == PackageStatus.AssignedForDelivery).ToArrayAsync();
            if(!result.Any())
            {
                return Ok("Jelenleg nincs kijelölt feladat!");
            }
            return Ok(result);
            
        


    }

}
}
