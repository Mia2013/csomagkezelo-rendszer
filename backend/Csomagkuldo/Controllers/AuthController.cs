using Csomagkuldo.Data;
using Csomagkuldo.DTOs;
using Csomagkuldo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Csomagkuldo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> passwordhaser;
        private readonly IConfiguration _config;
        public AuthController(AppDbContext context, PasswordHasher<User> passwordhaser, IConfiguration config)
        {
            _context = context;
            this.passwordhaser = passwordhaser;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody]RegisterDTO dto)
        {
            string userName = dto.UserName.Trim();
            string email = dto.Email.Trim();
            if (await _context.Users.AnyAsync(user => user.UserName == userName))
                return BadRequest("Ez a felhasználónév már létezik!");
            // Régi megoldás:
            // var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            // if(user!=null)
            // {
            //     return BadRequest("Ez az Email cím már létezik!");
            // }
            // Új megoldás:
            // Régi megoldás:
            // if (await _context.Users.AnyAsync(user => user.Email == dto.Email))
            // Új megoldás:
            if (await _context.Users.AnyAsync(user => user.Email == email))
            {
                return BadRequest("Ez az Email cím már létezik!");
            }
            User newuser = new()
            {
                // Régi megoldás:
                // UserName = dto.UserName ?? string.Empty,
                // Új megoldás:
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                LastName = dto.LastName ?? string.Empty,
                FirstName = dto.FirstName ?? string.Empty,
                // Régi megoldás:
                // Email = dto.Email ?? string.Empty,
                // Új megoldás:
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                Address = dto.Address ?? string.Empty,
                PhoneNumber = dto.PhoneNumber ?? string.Empty
            };
            newuser.PasswordHash = passwordhaser.HashPassword(newuser, dto.Password);
            // Régi megoldás:
            // await _context.Users.AddAsync(newuser);
            // Új megoldás:
            _context.Users.Add(newuser);
            await _context.SaveChangesAsync();
            return Ok("Sikeres regisztráció!");
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            // Régi megoldás:
            // var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName);
            // Új megoldás:
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName.Trim());
            if(user==null)
            {
                return NotFound("Ez a felhasználó nem található!");
            }
            if (!user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
                return Unauthorized("A bejelentkezés ezzel a felhasználóval nem engedélyezett.");

            var result = passwordhaser.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if(result==PasswordVerificationResult.Failed)
            {
                return Unauthorized("Hibás jelszó!");
            }
            return Ok(new { token = TokenGenerate(user) });
        }

        string TokenGenerate(User user)
        {
            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub, user.Email ?? ""),
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim("id", user.Id.ToString()),
        new Claim("firstName", user.FirstName),
        new Claim("role", user.Role.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
