using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }
        //MÁSIK FUTÁRHOZ FUTÁRHOZ RENDELÉS
        [Authorize(Roles = "Admin")]
        [HttpPut("packages/{packageid}/reassign")]
        public async Task<IActionResult> ReassignCourier(int packageid, [FromBody] AssignCourierRequestDTO dto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            if (package == null)
            {
                return NotFound("A csomag nem található!");
            }

            var newCourier = await _context.Users.FirstOrDefaultAsync(c => c.Id == dto.CourierId);
            if (newCourier == null)
            {
                return NotFound("Az új futár nem található!");
            }

            var currentTask = await _context.CourierTasks
                .FirstOrDefaultAsync(ct => ct.PackageId == packageid && ct.Status !=Enums.CourierTaskStatus.Completed);

            if (currentTask == null)
            {
                return BadRequest("A csomaghoz jelenleg nincs futárfeladat!");
            }

            if (currentTask.CourierId == dto.CourierId)
            {
                return BadRequest("A csomag már ehhez a futárhoz van rendelve!");
            }

            var capacityUnit = await _context.ShippingFees.Where(sf => sf.Type == package.Size).Select(sf => sf.CapacityUnit).FirstOrDefaultAsync();

            var newCar = await _context.CourierCars.FirstOrDefaultAsync(c => c.CourierId == dto.CourierId && c.Capacity >= capacityUnit);

            if (newCar == null)
            {
                return BadRequest("Az új futárhoz nincs autó rendelve, vagy nincs benne elég szabad hely!");
            }

            
            var oldCar = await _context.CourierCars.FirstOrDefaultAsync(c => c.CourierId == currentTask.CourierId);
            if (oldCar != null)
            {
                oldCar.Capacity += capacityUnit;
            }

            newCar.Capacity -= capacityUnit;
            currentTask.CourierId = newCourier.Id;
            currentTask.AssignmentTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok("A csomag sikeresen másik futárhoz lett rendelve!");
        }

    }
}
