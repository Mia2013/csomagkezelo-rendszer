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
            couriertask.TaskType = CourierTaskType.Pickup;

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
        public async Task<ActionResult<CourierTaskDTO[]>> GetCourirerPickUpTasks(int courierId)
        {
            var courier = await _context.Users.FindAsync(courierId);
            if (courier == null)
            {
                return NotFound("Futár nem található!");
            }
            var result = await _context.CourierTasks
                .Where(ct => ct.CourierId == courierId && ct.Status == CourierTaskStatus.Assigned && ct.Package.CurrentStatus == PackageStatus.AssignedForPickup)
                .Select(ct => new CourierTaskDTO
                {
                    Id = ct.Id,
                    PackageId = ct.PackageId,
                    TaskType = ct.TaskType,
                    Status = ct.Status,
                    AssignmentTime = ct.AssignmentTime,
                    TrackingNumber = ct.Package.TrackingNumber,
                    SenderName = ct.Package.SenderName,
                    SenderAddress = ct.Package.SenderAddress,
                    RecipientName = ct.Package.RecipientName,
                    RecipientAddress = ct.Package.RecipientAddress,
                    Size = ct.Package.Size,
                    PickupDate = ct.Package.PickupDate,
                    PickupSlot = ct.Package.PickupSlot
                })
                .ToArrayAsync();

            return Ok(result);
        }

        //FUTÁR MEGTEKINTHETI AZ ÖSSZES KISZÁLLÍTÁSI FELADATÁT
        [Authorize(Roles = "Courier,Admin")]
        [HttpGet("deliverytasks/{courierid}")]
        public async Task<ActionResult<CourierTaskDTO[]>> GetCourirerDeiveryTasks(int courierId)
        {
            var courier = await _context.Users.FindAsync(courierId);
            if (courier == null)
            {
                return NotFound("Futár nem található!");
            }

            var result = await _context.CourierTasks
                .Where(ct => ct.CourierId == courierId && ((ct.Status == CourierTaskStatus.Assigned && ct.Package.CurrentStatus == PackageStatus.AssignedForDelivery) || (ct.Status == CourierTaskStatus.InProgress && ct.Package.CurrentStatus == PackageStatus.OutForDelivery)))
                .Select(ct => new CourierTaskDTO
                {
                    Id = ct.Id,
                    PackageId = ct.PackageId,
                    TaskType = ct.TaskType,
                    Status = ct.Status,
                    AssignmentTime = ct.AssignmentTime,
                    TrackingNumber = ct.Package.TrackingNumber,
                    SenderName = ct.Package.SenderName,
                    SenderAddress = ct.Package.SenderAddress,
                    RecipientName = ct.Package.RecipientName,
                    RecipientAddress = ct.Package.RecipientAddress,
                    Size = ct.Package.Size,
                    PickupDate = ct.Package.PickupDate,
                    PickupSlot = ct.Package.PickupSlot
                })
                .ToArrayAsync();

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
            couriertask.TaskType = CourierTaskType.Delivery;

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
            switch (intervall)
            {
                case "daily":
                    startDate = now.Date;
                    break;
                case "weekly":
                    startDate = now.Date.AddDays(-7);
                    break;
                case "monthly":
                    startDate = now.Date.AddMonths(-1);
                    break;
                case "all":
                    startDate = DateTime.MinValue;
                    break;
                default:
                    break;
            }
            ;

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
        //SIKERTELEN KÉZBESÍTÉS
        [HttpPut("fail/{taskId}")]
        public async Task<IActionResult> FailDelivery(int taskId)
        {
            var couriertask = await _context.CourierTasks.Include(t => t.Package).FirstOrDefaultAsync(t => t.Id == taskId);

            if (couriertask == null)
            {
                return NotFound("A feladat nem található!");
            }
           

            if (couriertask.Package != null)
            {
                couriertask.Package.CurrentStatus = PackageStatus.FailedDelivery;

                couriertask.Package.StatusHistory.Add(new PackageStatusHistory
                {
                    Status = PackageStatus.FailedDelivery,
                    ChangedAt = DateTime.UtcNow
                });
            }
            couriertask.Package.CurrentStatus = PackageStatus.InTransitToWarehouse;

            couriertask.Package.StatusHistory.Add(new PackageStatusHistory
            {
                Status = PackageStatus.InTransitToWarehouse,
                ChangedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok("Sikertelen kézbesítés rögzítve.");
        }


    }
}
