using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Csomagkuldo.Enums;
using Csomagkuldo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }
        //MÁSIK FUTÁRHOZ FUTÁRHOZ RENDELÉS
        
        [HttpPut("packages/reassign/{packageid}")]
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
        //ÚJ FELHASZNÁLÓ LÉTREHOZÁSA
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
        {
            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                UserName = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                Role = dto.Role
            };

            PasswordHasher<User> passhasher = new PasswordHasher<User>();
            user.PasswordHash=passhasher.HashPassword(user, dto.Password);

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return Ok(user);
        }
        //ÖSSZES FELHASZNÁLÓK LEKÉRÉSE
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] UserRole? role)
        {
            var query =_context.Users.AsQueryable();

            if(query!=null)
            {
                query = query.Where(u => u.Role == role);
            }
            var users = query.Select(u => new UserProfileDTO
                {
                    
                 FirstName = u.FirstName,
                 LastName = u.LastName,
                 Email = u.Email,
                 PhoneNumber = u.PhoneNumber,
                 Address = u.Address,
                 Role=u.Role
                })
                .ToListAsync();

            return Ok(users);
        }
        //EGY FELHASZNÁLÓ ADATAINAK LEKÉRÉSE
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if(user==null)
            {
                return BadRequest("A felhasználó nem található");
            }
            var userProfile=new UserProfileDTO { 

                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Address = user.Address,
                    Role = user.Role
                };
                

            return Ok(userProfile);
        }
        //FELHASZNÁLÓ ADATAINAK MÓDOSÍTÁSA
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDTO dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Email = dto.Email;
            user.UserName = dto.Email;
            user.NormalizedEmail = dto.Email.ToUpper();
            user.NormalizedUserName = dto.Email.ToUpper();
            user.PhoneNumber = dto.PhoneNumber;
            user.Address = dto.Address;
            user.Role = dto.Role;

           
            if (!string.IsNullOrEmpty(dto.Password))
            {
                PasswordHasher<User> passhasher = new PasswordHasher<User>();
                user.PasswordHash = passhasher.HashPassword(user, dto.Password);
            }

            await _context.SaveChangesAsync();

            return Ok();
        }
        //FELHASZNÁLÓ TÖRLÉSE
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok();
        }
        //ÖSSZES AZTÓ ADATAINAK LEKÉRÉSE
        [HttpGet("cars")]
        public async Task<IActionResult> GetAllCars()
        {
            var cars = await _context.CourierCars
                .Select(c => new CourierCarDTO
                {
                    Id = c.Id,
                    Capacity = c.Capacity,
                    CourierId = c.CourierId,
                    CourierName = c.Courier != null ? c.Courier.LastName + " " + c.Courier.FirstName : null,
                    LicensePlate = c.LicensePlate,
                    Manufacturer = c.Manufacturer,
                    Model = c.Model,
                    Year = c.Year
                })
                .ToListAsync();

            return Ok(cars);
        }

        //EGY AUTÓ ADATAINAK LEKÉRÉSE
        [HttpGet("cars/{id}")]
        public async Task<IActionResult> GetCar(int id)
        {
            var car = await _context.CourierCars
                .Where(c => c.Id == id)
                .Select(c => new CourierCarDTO
                {
                    Id = c.Id,
                    Capacity = c.Capacity,
                    CourierId = c.CourierId,
                    CourierName = c.Courier != null ? c.Courier.LastName + " " + c.Courier.FirstName : null,
                    LicensePlate = c.LicensePlate,
                    Manufacturer = c.Manufacturer,
                    Model = c.Model,
                    Year = c.Year
                })
                .FirstOrDefaultAsync();

            if (car == null) return NotFound();

            return Ok(car);
        }

        //ÚJ AUTÓ HOZZÁADÁSA
        [HttpPost("cars")]
        public async Task<IActionResult> CreateCar([FromBody] CreateUpdateCarDTO dto)
        {
            var car = new CourierCar
            {
                Capacity = dto.Capacity,
                CourierId = dto.CourierId,
                LicensePlate = dto.LicensePlate,
                Manufacturer = dto.Manufacturer,
                Model = dto.Model,
                Year = dto.Year
            };

            _context.CourierCars.Add(car);
            await _context.SaveChangesAsync();

            return Ok(car);
        }

        //KOCSI ADATAINAK MÓDOSÍTÁSA
        [HttpPut("cars/{id}")]
        public async Task<IActionResult> UpdateCar(int id, [FromBody] CreateUpdateCarDTO dto)
        {
            var car = await _context.CourierCars.FindAsync(id);
            if (car == null) return NotFound();

            car.Capacity = dto.Capacity;
            car.CourierId = dto.CourierId;
            car.LicensePlate = dto.LicensePlate;
            car.Manufacturer = dto.Manufacturer;
            car.Model = dto.Model;
            car.Year = dto.Year;

            await _context.SaveChangesAsync();

            return Ok();
        }

        //AUTÓ TÖRLÉSE
        [HttpDelete("cars/{id}")]
        public async Task<IActionResult> DeleteCar(int id)
        {
            var car = await _context.CourierCars.FindAsync(id);
            if (car == null) return NotFound();

            _context.CourierCars.Remove(car);
            await _context.SaveChangesAsync();

            return Ok();
        }
        
        //ÖSSZES CSOMAG LEKÉRÉSE
        [HttpGet("packages")]
        public async Task<IActionResult> GetAllPackages()
        {
            var packages = await _context.Packages
                .Select(p => new PackageListItemDTO
                {
                    Id = p.Id,
                    TrackingNumber = p.TrackingNumber,
                    SenderPhoneNumber = p.SenderPhoneNumber,
                    RecipientPhoneNumber = p.RecipientPhoneNumber,
                    RecipientEmail = p.RecipientEmail,
                    PickupSlot = p.PickupSlot,
                    Value = p.Value,
                    Weight = p.Weight,
                    Size = p.Size,
                    SenderName = p.SenderName,
                    SenderAddress = p.SenderAddress,
                    RecipientName = p.RecipientName,
                    RecipientAddress = p.RecipientAddress,
                    CurrentStatus = p.CurrentStatus,
                    PickupDate = p.PickupDate,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return Ok(packages);
        }
        //EGY CSOMAG ADATAINAK LEKÉRÉSE
        [HttpGet("packages/{id}")]
        public async Task<IActionResult> GetPackage(int id)
        {
            var package = await _context.Packages
                .Where(p => p.Id == id)
                .Select(p => new PackageListItemDTO
                {
                    Id = p.Id,
                    TrackingNumber = p.TrackingNumber,
                    SenderPhoneNumber = p.SenderPhoneNumber,
                    RecipientPhoneNumber = p.RecipientPhoneNumber,
                    RecipientEmail = p.RecipientEmail,
                    PickupSlot = p.PickupSlot,
                    Value = p.Value,
                    Weight = p.Weight,
                    Size = p.Size,
                    SenderName = p.SenderName,
                    SenderAddress = p.SenderAddress,
                    RecipientName = p.RecipientName,
                    RecipientAddress = p.RecipientAddress,
                    CurrentStatus = p.CurrentStatus,
                    PickupDate = p.PickupDate,
                    CreatedAt = p.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (package == null) return NotFound();

            return Ok(package);
        }
        //CSOMAG LÉTREHOZÁSA ADMIN ÁLTAL
        [HttpPost("packages")]
        public async Task<IActionResult> CreatePackage([FromBody] AdminPackageDTO dto)
        {
            DateTime now = DateTime.UtcNow;

            var package = new Package
            {
                TrackingNumber = Guid.NewGuid().ToString("N")[..10].ToUpper(), 
                SenderId = dto.SenderId, 
                SenderName = dto.SenderName.Trim(),
                SenderAddress = dto.SenderAddress.Trim(),
                SenderPhoneNumber = dto.SenderPhoneNumber?.Trim(),
                RecipientId = dto.RecipientId,
                RecipientName = dto.RecipientName.Trim(),
                RecipientAddress = dto.RecipientAddress.Trim(),
                RecipientPhoneNumber = dto.RecipientPhoneNumber?.Trim(),
                RecipientEmail = dto.RecipientEmail?.Trim(),
                Value = dto.Value,
                Weight = dto.Weight,
                Size = dto.Size,
                PickupDate = dto.PickupDate,
                PickupSlot = dto.PickupSlot,
                CurrentStatus = dto.CurrentStatus,
                CourierId = dto.CourierId,
                CreatedAt = now
            };

            
            package.StatusHistory.Add(new PackageStatusHistory
            {
                Status = package.CurrentStatus,
                ChangedAt = now
            });

            _context.Packages.Add(package);
            await _context.SaveChangesAsync();

            return Ok(new PackageCreatedDTO
            {
                TrackingNumber = package.TrackingNumber,
                Status = package.CurrentStatus,
                CreatedAt = package.CreatedAt
            });
        }

        //CSOMAG ADATAINAK MÓDOSÍTÁSA
        [HttpPut("packages/{id}")]
        public async Task<IActionResult> UpdatePackage(int id, [FromBody] AdminPackageDTO dto)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package == null) return NotFound();

            package.SenderPhoneNumber = dto.SenderPhoneNumber;
            package.RecipientPhoneNumber = dto.RecipientPhoneNumber;
            package.RecipientEmail = dto.RecipientEmail;
            package.PickupSlot = dto.PickupSlot;
            package.Value = dto.Value;
            package.Weight = dto.Weight;
            package.Size = dto.Size;
            package.SenderId = dto.SenderId;
            package.SenderName = dto.SenderName;
            package.SenderAddress = dto.SenderAddress;
            package.RecipientId = dto.RecipientId;
            package.RecipientName = dto.RecipientName;
            package.RecipientAddress = dto.RecipientAddress;
            package.CurrentStatus = dto.CurrentStatus;
            package.CourierId = dto.CourierId;
            package.PickupDate = dto.PickupDate;

            await _context.SaveChangesAsync();

            return Ok();
        }
        //CSOMAG TÖRLÉSE
        [HttpDelete("packages/{id}")]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var package = await _context.Packages.FindAsync(id);
            if (package == null) return NotFound();

            _context.Packages.Remove(package);
            await _context.SaveChangesAsync();

            return Ok();
        }

        //ÖSSZES SHIPPINGFEE ADAT LEKÉRÉSE
        [HttpGet("shippingfees")]
        public async Task<IActionResult> GetAllShippingFees()
        {
            var shippingfees = await _context.ShippingFees.Select(sf => new ShippingFeeDTO
                {
                    Id = sf.Id,
                    Type = sf.Type,
                    MaxSize = sf.MaxSize,
                    RegisteredPrice = sf.RegisteredPrice,
                    UnregisteredPrice = sf.UnregisteredPrice,
                    CapacityUnit = sf.CapacityUnit
                })
                .ToListAsync();

            return Ok(shippingfees);
        }

        //SZÁLLÍTÁSI DÍJ MÓDOSÍTÁSA
        [HttpPut("shippingfees/{id}")]
        public async Task<IActionResult> UpdateShippingFee(int id, [FromBody] UpdateShippingFeeDTO dto)
        {
            var fee = await _context.ShippingFees.FindAsync(id);
            if (fee == null)
            { 
                return NotFound("Ez a költség nem található!");
            }


            fee.MaxSize = dto.MaxSize;
            fee.RegisteredPrice = dto.RegisteredPrice;
            fee.UnregisteredPrice = dto.UnregisteredPrice;
            fee.CapacityUnit = dto.CapacityUnit;

            await _context.SaveChangesAsync();

            return Ok();
        }
        // FUTÁRFELADATOK LEKÉRÉSE STÁTUSZ SZERINT
        [HttpGet("couriertasks")]
        public async Task<IActionResult> GetCourierTasks([FromQuery] CourierTaskStatus? status)
        {
            var query = _context.CourierTasks.AsQueryable();

            
            if (status!=null)
            {
                query = query.Where(ct => ct.Status == status);
            }

            var tasks = await query.Select(ct => new
            {
                ct.Id,
                ct.PackageId,
                ct.CourierId,
                CourierName = ct.Courier.LastName + " " + ct.Courier.FirstName,
                ct.Status,
                ct.AssignmentTime
            })
              .ToListAsync();

            return Ok(tasks);
        }

    }
}
