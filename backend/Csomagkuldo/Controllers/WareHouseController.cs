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
        [Authorize(Roles = "WarehouseOperator,Admin")]
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
        //BEÉRKEZŐ / RAKTÁRBAN LÉVŐ CSOMAGOK LISTÁJA
        [Authorize(Roles = "WarehouseOperator,Admin")]
        [HttpGet("packages")]
        public async Task<IActionResult> GetPackagesByStatus([FromQuery] PackageStatus status)
        {
            if (status != PackageStatus.InTransitToWarehouse && status != PackageStatus.AtWarehouse)
            {
                return BadRequest("Csak a beérkező és a raktárban lévő csomagok kérhetők le!");
            }

            var result = await _context.Packages.AsNoTracking()
                .Where(p => p.CurrentStatus == status)
                .OrderBy(p => p.PickupDate)
                .Select(p => new WarehousePackageDTO
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
                    CourierName = p.CourierTasks
                        .Where(ct => ct.Status == CourierTaskStatus.InProgress)
                        .Select(ct => ct.Courier.LastName + " " + ct.Courier.FirstName)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(result);
        }
        //ÖSSZES FUTÁR LEKÉRÉSE
        [HttpGet("couriers")]
        public async Task<IActionResult> GetAllCouriers()
        {
            var couriers = await _context.Users.Where(u => u.Role == UserRole.Courier).Select(u => new
            {
                u.Id,
                FullName = u.LastName + " " + u.FirstName,
                u.PhoneNumber,
                u.Email
            })
                .ToListAsync();

            return Ok(couriers);
        }
        //CSOMAG FUTÁRHOZ RENDELÉSE
        [Authorize(Roles = "WarehouseOperator,Admin")]
        [HttpPut("assignforpickup/{packageid}")]
        public async Task<IActionResult> AssignCourierToPackage(int packageid, [FromBody] AssignCourierRequestDTO courierdto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == courierdto.CourierId);
            if (package == null || courier == null)
            {
                return BadRequest("A hozzárendelés nem hajtható végre!");
            }
            if (courier.Role != UserRole.Courier)
            {
                return BadRequest("A kiválasztott felhasználó nem futár!");
            }
            if (package.CurrentStatus != PackageStatus.PreBooked)
            {
                return BadRequest("Csak előjegyzett csomag osztható ki felvételre!");
            }

            var capacityUnit = await _context.ShippingFees.Where(s => s.Type == package.Size).Select(u => u.CapacityUnit).FirstOrDefaultAsync();
            var Car = await _context.CourierCars.Where(c => c.Capacity >= capacityUnit && c.CourierId == courierdto.CourierId).FirstOrDefaultAsync();
            if (Car == null)
            {
                return BadRequest("Az autó elérte maximum kapacitását!");
            }
            
          
                await _context.CourierTasks.AddAsync(new CourierTask
                {
                    PackageId = packageid,
                    TaskType = CourierTaskType.Pickup,
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
                Car.Capacity -= capacityUnit;
                package.CurrentStatus = PackageStatus.AssignedForPickup;

                await _context.SaveChangesAsync();
                return Ok("Futár sikeresen  a csomaghoz lett rendelve!");
            
          
        }
        
        //FUTÁR HOZZÁRENDELÉSE A CSOMAG KISZÁLLÍTÁSÁHOZ
        [Authorize(Roles = "WarehouseOperator,Admin")]
        [HttpPut("assignfordelivery/{packageid}")]
        public async Task<IActionResult> Delivery(int packageid, [FromBody] AssignCourierRequestDTO dto)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            var courier = await _context.Users.FirstOrDefaultAsync(c => c.Id == dto.CourierId);
            if (package == null || courier == null)
            {
                return BadRequest("Hozzárendelés nem hajtható végre!");

            }
            if (courier.Role != UserRole.Courier)
            {
                return BadRequest("A kiválasztott felhasználó nem futár!");
            }
            if (package.CurrentStatus != PackageStatus.AtWarehouse)
            {
                return BadRequest("Csak raktárban lévő csomag osztható ki kiszállításra!");
            }

            var capacityUnit = await _context.ShippingFees.Where(s => s.Type == package.Size).Select(u => u.CapacityUnit).FirstOrDefaultAsync();
            var Car = await _context.CourierCars.Where(c => c.Capacity >= capacityUnit && c.CourierId ==dto.CourierId).FirstOrDefaultAsync();
            if (Car == null)
            {
                return BadRequest("Az autóba elérte maximum kapacitását!");
            }
            package.CurrentStatus = PackageStatus.AssignedForDelivery;
            Car.Capacity -= capacityUnit;
            await _context.CourierTasks.AddAsync(new CourierTask
            {
                PackageId = packageid,
                TaskType = CourierTaskType.Delivery,
                Status = CourierTaskStatus.Assigned,
                CourierId = courier.Id,
                AssignmentTime = DateTime.UtcNow

            });
            await _context.PackageStatuses.AddAsync(new PackageStatusHistory
            {
                PackageId = packageid,
                Status = PackageStatus.AssignedForDelivery,
                ChangedAt = DateTime.UtcNow
            });

            
            await _context.SaveChangesAsync();
            return Ok("Futár sikeresen  a csomaghoz lett rendelve!");

        }
        //CSOMAG MEGÉRKEZIK A RAKTÁRBA
        [Authorize(Roles = "WarehouseOperator,Admin")]
        [HttpPut("receivetowarehouse/{packageid}")]
        public async Task<IActionResult> ReceiveToWarehouse(int packageid)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Id == packageid);
            if (package == null)
            {
                return NotFound("A csomag nem található!");
            }
            if (package.CurrentStatus != PackageStatus.InTransitToWarehouse)
            {
                return BadRequest("Csak raktár felé tartó csomag vételezhető be!");
            }

            var couriertask = await _context.CourierTasks.FirstOrDefaultAsync(c => c.PackageId == packageid && c.Status == CourierTaskStatus.InProgress);

            if (couriertask == null)
            {
                return NotFound("A futár feladat nem található!");
            }

            var couriercar = await _context.CourierCars.FirstOrDefaultAsync(c => c.CourierId == couriertask.CourierId);
            if (couriercar != null)
            {
                var capacityUnit = await _context.ShippingFees.Where(sf => sf.Type == package.Size).Select(sf => sf.CapacityUnit).FirstOrDefaultAsync();

                couriercar.Capacity += capacityUnit;
            }

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
    }
}
