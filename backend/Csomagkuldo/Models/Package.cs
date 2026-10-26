using Csomagkuldo.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Csomagkuldo.Models
{
    public class Package
    {
        public int Id { get; set; }

        public string TrackingNumber { get; set; } = string.Empty;

        // Régi csomagoknál ezek az új adatok még hiányozhatnak.
        public string? SenderPhoneNumber { get; set; }
        public string? RecipientPhoneNumber { get; set; }
        public string? RecipientEmail { get; set; }
        public string? PickupSlot { get; set; }

        public int Value { get; set; }

        public int Weight { get; set; }

        public string Size { get; set; } = string.Empty;

        public bool IsPaid { get; set; } = false; //0-még nem fizetett, 1-kifizetve

        // Régi csomagoknál a díj nem ismert; új feladásnál mindkettőt kitöltjük. Ezt még lehet veriálni kell majd.
        public int? ShippingFeeId { get; set; }
        public int? ShippingCost { get; set; }

        // vendég feladó
        public int? SenderId { get; set; }

        public string SenderName { get; set; } = string.Empty;

        public string SenderAddress { get; set; } = string.Empty;

        // vendég címzett
        public int? RecipientId { get; set; }

        public string RecipientName { get; set; } = string.Empty;

        public string RecipientAddress { get; set; } = string.Empty;

        [ConcurrencyCheck]
        public PackageStatus CurrentStatus { get; set; }
            = PackageStatus.PreBooked;

        // új csomag
        [ConcurrencyCheck]
        public int? CourierId { get; set; }

        public DateTime PickupDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ShippingFee? ShippingFee { get; set; }

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
