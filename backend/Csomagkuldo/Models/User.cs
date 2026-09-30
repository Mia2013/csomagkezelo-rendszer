using Csomagkuldo.Enums;

namespace Csomagkuldo.Models
{
    public class User
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.Customer;

        //a felhasználó adott fel
        public List<Package> SentPackages { get; set; }
            = new List<Package>();

        //felhasználó a címzett
        public List<Package> ReceivedPackages { get; set; }
            = new List<Package>();

        //aktuálisan hozzá rendelt csomagok futárhoz
        public List<Package> AssignedPackages { get; set; }
            = new List<Package>();

        //futár feladatok
        public List<CourierTask> CourierTasks { get; set; }
            = new List<CourierTask>();

        //felhasználó mint futár
        public List<PackageStatusHistory> PackageStatusChanges { get; set; }
            = new List<PackageStatusHistory>();
    }
}
