using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Csomagkuldo.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Csomagkuldo.Controllers

{
    //figyelni, hogy melyik controllert milyen jogosultsággal lehet elérni
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UserController(AppDbContext context)
        {
            _context = context;
        }
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _context.Users.AsNoTracking().Select(user => new UserDTO
            {
                Id = user.Id, UserName = user.UserName,
                FirstName = user.FirstName, LastName = user.LastName,
                Email = user.Email, Address = user.Address, PhoneNumber = user.PhoneNumber,
                Role = user.Role, IsActive = user.IsActive
            }).ToListAsync();
            return Ok(users);
        }

        [Authorize(Roles = "WarehouseOperator,Admin")]
        [HttpGet("couriers")]
        public async Task<IActionResult> GetCouriers()
        {
            var couriers = await _context.Users.AsNoTracking()
                .Where(user => user.Role == UserRole.Courier && user.IsActive)
                .OrderBy(user => user.LastName).ThenBy(user => user.FirstName).ThenBy(user => user.Id)
                .Select(user => new UserDTO
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                }).ToListAsync();
            return Ok(couriers);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (User.FindFirst("id")?.Value != id.ToString() && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var user = await _context.Users.AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new UserDTO
                {
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email ?? string.Empty,
                    Address = u.Address,
                    PhoneNumber = u.PhoneNumber
                })
                .FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound($"Az {id} azonosítójú felhasználó nem található!");
            }
            return Ok(user);
        }


        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // Régi megoldás:
            // // Régi megoldás:
            // // var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            // // Új megoldás:
            // var user = await _context.Users.FindAsync(id);
            // if (user == null)
            // {
            //     return NotFound($"A(z) {id} azonosítójú felhasználó nem található!");
            //
            // }
            // _context.Users.Remove(user);
            // await _context.SaveChangesAsync();
            // return Ok($"A(z) {id} azonosítójú felhasználó sikeresen törlésre került!");
            // Új megoldás:
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound("A felhasználó nem található!");

            bool referenced = await _context.Packages.AnyAsync(package =>
                    package.SenderId == id || package.RecipientId == id || package.CourierId == id) ||
                await _context.CourierTasks.AnyAsync(task => task.CourierId == id) ||
                await _context.PackageStatuses.AnyAsync(status => status.CourierId == id) ||
                await _context.CourierCars.AnyAsync(car => car.CourierId == id);
            if (referenced)
                return Conflict("A felhasználóhoz csomag, futárfeladat, előzmény vagy autó tartozik, ezért nem törölhető.");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok("A felhasználót sikeresen töröltük.");
        }


    }

}
