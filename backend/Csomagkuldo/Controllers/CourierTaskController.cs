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
        //FUTÁR FELVESZI A FELADOTT CSOMAGOT
        [Authorize(Roles = "Courier,Admin")]
        [HttpPut("pickuppackage/{packageid}")]
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
        [HttpGet("pickuptasks/{courierid}")]
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
        [HttpGet("deliverytasks/{courierid}")]
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
        //A FUTÁR FELVESZI A CSOMAGOT ÉS ELINDUL A RAKTÁRBÓL
        
        [Authorize(Roles = "Courier,Admin")]
        [HttpPut("pickupfromwarehouse/{packageid}")]
        public async Task<IActionResult> PickupFromWarehouse(int packageid, [FromBody] CourierDTO dto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            if (package == null)
            {
                return NotFound("Csomag nem található!");
            }

            var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == dto.CourierId);
            if (courier == null)
            {
                return NotFound("Futár nem található!");
            }

            var couriertask = await _context.CourierTasks.FirstOrDefaultAsync(ct => ct.PackageId == packageid&& ct.CourierId == dto.CourierId&& ct.Status == CourierTaskStatus.Assigned&& ct.Package.CurrentStatus == PackageStatus.AssignedForDelivery);

            if (couriertask == null)
            {
                return NotFound("A futárhoz nem tartozik feladat!");
            }

           
            couriertask.Status = CourierTaskStatus.InProgress;
            package.CurrentStatus = PackageStatus.OutForDelivery;

            await _context.PackageStatuses.AddAsync(new PackageStatusHistory
            {
                PackageId = packageid,
                Status = PackageStatus.OutForDelivery,
                ChangedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok("A futár elindult a csomaggal a raktárból!");
        }
        //CsOMAG KÉZBESÍTÉSE
        [Authorize(Roles = "Courier,Admin")]
        [HttpPut("deliver/{packageid}")]
        public async Task<IActionResult> DeliverPackage(int packageid, [FromBody] CourierDTO dto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            if (package == null)
            {
                return NotFound("Csomag nem található!");
            }

            var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == dto.CourierId);
            if (courier == null)
            {
                return NotFound("Futár nem található!");
            }

            var couriertask = await _context.CourierTasks.FirstOrDefaultAsync(ct => ct.PackageId == packageid&& ct.CourierId == dto.CourierId&& ct.Status == CourierTaskStatus.InProgress);

            if (couriertask == null)
            {
                return NotFound("Nincs ilyen futárfeladat!");
            }

            var couriercar = await _context.CourierCars.FirstOrDefaultAsync(c => c.CourierId == dto.CourierId);
            if (couriercar != null)
            {
                var capacityUnit = await _context.ShippingFees.Where(s => s.Type == package.Size).Select(u => u.CapacityUnit).FirstOrDefaultAsync();

                couriercar.Capacity += capacityUnit;
            }

            couriertask.Status = CourierTaskStatus.Completed;
            package.CurrentStatus = PackageStatus.Delivered;

            await _context.PackageStatuses.AddAsync(new PackageStatusHistory
            {
                PackageId = packageid,
                Status = PackageStatus.Delivered,
                ChangedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok("Csomag sikeresen kézbesítve!");
        }
        // FUTÁR SZÁLLÍTÁSI TÖRTÉNETE NAPI / HETI / HAVI 
        [Authorize(Roles = "Courier,Admin")]
        [HttpGet("history/{courierId}")]
        public async Task<IActionResult> GetCourierHistory(int courierId, [FromQuery] string intervall)
        {
            var courier = await _context.Users.FindAsync(courierId);
            if (courier == null)
            {
                return NotFound("A futár nem található!");
            }

            DateTime now = DateTime.UtcNow;
            DateTime startDate = new DateTime();
            switch(intervall)
            {
                case "daily":
                    startDate=now.Date;
                        break;
                case "weekly":
                    startDate = now.Date.AddDays(-7);
                    break;
                case "monthly":
                    startDate = now.Date.AddMonths(-1);
                    break;
                default:
                    break;
            };

            var tasks = await _context.CourierTasks.Where(ct => ct.CourierId == courierId&& ct.Status == CourierTaskStatus.Completed&& ct.AssignmentTime >= startDate)
                .Select(ct => new
                {
                    TaskId = ct.Id,
                    PackageId = ct.Package.Id,
                    TrackingNumber = ct.Package.TrackingNumber,
                    AssignmentTime = ct.AssignmentTime,
                    PackageStatus = ct.Package.CurrentStatus,
                    SenderAddress = ct.Package.SenderAddress,
                    RecipientAddress = ct.Package.RecipientAddress
                })
                .OrderByDescending(ct => ct.AssignmentTime)
                .ToListAsync();

            return Ok(new
            {
                Period = intervall.ToLower(),
                TotalCompletedTasks = tasks.Count,
                Tasks = tasks
            });
        }


    }
}
