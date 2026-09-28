using Csomagkuldo_Backend.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Csomagkuldo_Backend.Models
{
    public class Package
    {
        public int Id { get; set; }

        public string TrackingNumber { get; set; } = string.Empty;

        public int Value { get; set; }

        public int Weight { get; set; }

        public string Size { get; set; } = string.Empty;

        // vendég feladó
        public int? SenderId { get; set; }

        public string SenderName { get; set; } = string.Empty;

        public string SenderAddress { get; set; } = string.Empty;

        // vendég címzett
        public int? RecipientId { get; set; }

        public string RecipientName { get; set; } = string.Empty;

        public string RecipientAddress { get; set; } = string.Empty;

        public PackageStatus CurrentStatus { get; set; }
            = PackageStatus.PreBooked;

        // új csomag
        public int? CourierId { get; set; }

        public DateTime PickupDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SenderId")]
        [InverseProperty("SentPackages")]
        public User? Sender { get; set; }

        [ForeignKey("RecipientId")]
        [InverseProperty("ReceivedPackages")]
        public User? Recipient { get; set; }

        [ForeignKey("CourierId")]
        [InverseProperty("AssignedPackages")]
        public User? Courier { get; set; }

        public List<CourierTask> CourierTasks { get; set; }
            = new List<CourierTask>();

        public List<PackageStatusHistory> StatusHistory { get; set; }
            = new List<PackageStatusHistory>();
    }
}
