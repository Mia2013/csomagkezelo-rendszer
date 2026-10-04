using System.ComponentModel.DataAnnotations;

namespace Csomagkuldo.DTOs;

public class LoginDTO
{
    [Required]
    public string UserName { get; set; } = string.Empty;
    [Required]
    public string Password { get; set; } = string.Empty;
}
