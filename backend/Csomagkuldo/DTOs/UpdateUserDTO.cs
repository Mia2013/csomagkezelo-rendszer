using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs
{
    public class UpdateUserDTO
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Address { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string? Password { get; set; }
    }
}
