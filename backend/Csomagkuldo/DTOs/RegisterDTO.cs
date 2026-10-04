using System.ComponentModel.DataAnnotations;

namespace Csomagkuldo.DTOs;

public class RegisterDTO
{
    [Required, StringLength(100)]
    public string UserName { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string LastName { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [StringLength(30)]
    public string? PhoneNumber { get; set; }
    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;
}
