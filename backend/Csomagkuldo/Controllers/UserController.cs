using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Controllers

{
    //figyelni, hogy melyik controllert milyen jogosultsággal lehet elérni
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : Controller
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
            var users = await _context.Users.ToListAsync();
            return Ok(users);
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
                .Select(u => new UserProfileDTO
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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound($"A(z) {id} azonosítójú felhasználó nem található!");

            }
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok($"A(z) {id} azonosítójú felhasználó sikeresen törlésre került!");
        }


    }

}
