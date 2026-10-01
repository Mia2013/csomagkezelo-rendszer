using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Csomagkuldo.Data;
using Csomagkuldo.Models;
using Microsoft.EntityFrameworkCore;
using Csomagkuldo.DTOs;

namespace Csomagkuldo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> passwordhaser;
        public AuthController(AppDbContext context, PasswordHasher<User> passwordhaser)
        {
            _context = context;
            this.passwordhaser = passwordhaser;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody]RegisterDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if(user!=null)
            {
                return BadRequest("Ez az Email cím már létezik!");
            }
            User newuser = new User
            {
                UserName = dto.UserName,
                LastName = dto.LastName,
                FirstName = dto.FirstName,
                Email = dto.Email,
            };
            newuser.PasswordHash = passwordhaser.HashPassword(newuser, dto.Password);
            return Ok("Sikeres regisztráció!");  
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName);
            if(user==null)
            {
                return NotFound("Ez a felhasználó nem található!");
            }
            var result = passwordhaser.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if(result==PasswordVerificationResult.Failed)
            {
                return Unauthorized("Hibás jelszó!");
            }
            return Ok("Sikeresen bejelentkezett!");
        }
    }
}
