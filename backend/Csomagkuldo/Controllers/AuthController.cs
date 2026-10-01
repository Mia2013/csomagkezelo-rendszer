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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if(user!=null)
            {
                return BadRequest("Ez az Email cím már létezik!");
            }
            User newuser = new()
            {
                UserName = dto.UserName ?? string.Empty,
                LastName = dto.LastName ?? string.Empty,
                FirstName = dto.FirstName ?? string.Empty,
                Email = dto.Email ?? string.Empty,
                Address = dto.Address ?? string.Empty,
                PhoneNumber = dto.PhoneNumber ?? string.Empty
            };
            newuser.PasswordHash = passwordhaser.HashPassword(newuser, dto.Password);
            await _context.Users.AddAsync(newuser);
            await _context.SaveChangesAsync();
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
