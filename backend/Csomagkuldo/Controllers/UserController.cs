using Csomagkuldo.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Controllers

{
    [Route("api/controller")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly AppDbContext _context;
        public UserController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult>GetAll()
        {
            var users = await _context.Users.ToListAsync();
            return Ok(users);
        }
        [HttpGet("id")]
        public async Task<IActionResult>GetById(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound($"Az {id} azonosítójú felhasználó nem található!");

            }
            return Ok(user);
        }
        [HttpDelete("id")]
        public async Task<IActionResult>Delete(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if(user==null)
            {
                return NotFound($"A(z) {id} azonosítójú felhasználó nem található!");
                
            }
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok($"A(z) {id} azonosítójú felhasználó sikeresen törlésre került!");
        }
        
    }
}
