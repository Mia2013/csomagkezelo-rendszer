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

namespace Csomagkuldo.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, PasswordHasher<User> passwordHasher, IConfiguration config)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDTO dto)
    {
        string name = dto.UserName.Trim();
        string email = dto.Email.Trim();
        if (await _context.Users.AnyAsync(user => user.UserName == name || user.Email == email))
        {
            return BadRequest("Ez a felhasználónév vagy e-mail-cím már foglalt.");
        }

        var user = new User
        {
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Address = dto.Address.Trim(),
            PhoneNumber = dto.PhoneNumber?.Trim()
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok("Sikeres regisztráció!");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(user => user.UserName == dto.UserName.Trim());
        if (user == null || user.PasswordHash == null ||
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized("Hibás felhasználónév vagy jelszó.");
        }
        return Ok(new { token = GenerateToken(user) });
    }

    private string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("id", user.Id.ToString()),
            new Claim("firstName", user.FirstName),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
