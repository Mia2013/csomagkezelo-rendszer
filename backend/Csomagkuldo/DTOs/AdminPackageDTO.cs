using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs
{
    public class AdminPackageDTO
    {
        public int? SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderAddress { get; set; } = string.Empty;
        public string? SenderPhoneNumber { get; set; }

        // Címzett adatai (RecipientId null, ha vendég)
        public int? RecipientId { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientAddress { get; set; } = string.Empty;
        public string? RecipientPhoneNumber { get; set; }
        public string? RecipientEmail { get; set; }

        public int Value { get; set; }
        public int Weight { get; set; }
        public PackageSize Size { get; set; }

        public DateTime PickupDate { get; set; }
        public string? PickupSlot { get; set; }

        public PackageStatus CurrentStatus { get; set; } = PackageStatus.PreBooked;
        public int? CourierId { get; set; }
    }
}

