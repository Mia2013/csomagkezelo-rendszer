using Csomagkuldo.Enums;
using System.ComponentModel.DataAnnotations;

namespace Csomagkuldo.DTOs
{
    public class CreateUserDTO
    {

        public string? FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        [MaxLength(200)]
        public string Address { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; } = UserRole.Customer;
    }
}

