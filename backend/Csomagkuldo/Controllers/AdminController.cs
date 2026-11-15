using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
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
        [HttpPost]
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
        //FELHASZNÁLÓK LEKÉRÉSE
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new UserProfileDTO
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
        [HttpPut("{id}")]
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
