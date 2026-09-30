using Csomagkuldo.Enums;
using Microsoft.AspNetCore.Identity;

namespace Csomagkuldo.Models
{
    public class User : IdentityUser<int>
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.Customer;

        public List<Package> SentPackages { get; set; }
            = new List<Package>();

        public List<Package> ReceivedPackages { get; set; }
            = new List<Package>();

        public List<Package> AssignedPackages { get; set; }
            = new List<Package>();

        public List<CourierTask> CourierTasks { get; set; }
            = new List<CourierTask>();

        public List<PackageStatusHistory> PackageStatusChanges { get; set; }
            = new List<PackageStatusHistory>();
    }
}
