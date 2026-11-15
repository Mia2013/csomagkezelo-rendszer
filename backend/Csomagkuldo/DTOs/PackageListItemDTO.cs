using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs;

public class PackageListItemDTO
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public PackageSize Size { get; set; }
    public DateTime PickupDate { get; set; }
    public string? PickupSlot { get; set; }
    public PackageStatus CurrentStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Value { get; set; }
    public int Weight { get; set; }
    public string? SenderPhoneNumber { get; set; }
    public string? RecipientPhoneNumber { get; set; }
    public string? RecipientEmail { get; set; }
    public bool IsSender { get; set; }
}
