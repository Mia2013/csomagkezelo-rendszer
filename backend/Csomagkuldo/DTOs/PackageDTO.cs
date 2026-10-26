using System.ComponentModel.DataAnnotations;
using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs;

// A szükséges mezőket mindig az adott művelet ellenőrzi.
public class PackageDTO
{
    public int? Id { get; set; }
    public string? TrackingNumber { get; set; }

    [StringLength(150)]
    public string? SenderName { get; set; }
    [StringLength(300)]
    public string? SenderAddress { get; set; }
    [StringLength(30), Phone]
    public string? SenderPhoneNumber { get; set; }

    [StringLength(150)]
    public string? RecipientName { get; set; }
    [StringLength(300)]
    public string? RecipientAddress { get; set; }
    [StringLength(30), Phone]
    public string? RecipientPhoneNumber { get; set; }
    [StringLength(254), EmailAddress]
    public string? RecipientEmail { get; set; }

    [Range(0, int.MaxValue)]
    public int? Value { get; set; }
    [Range(1, 30000)]
    public int? Weight { get; set; }
    [RegularExpression("^(S|M|L)$")]
    public string? Size { get; set; }
    public DateTime? PickupDate { get; set; }
    [RegularExpression("^(09:00-12:00|12:00-16:00)$")]
    public string? PickupSlot { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Adj meg egy érvényes futárazonosítót!")]
    public int? CourierId { get; set; }
    public PackageStatus? CurrentStatus { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ChangedAt { get; set; }
    public int? ShippingCost { get; set; }
    public bool? IsPaid { get; set; }
    public bool? CanReassign { get; set; }
    public bool? CanReceive { get; set; }
    public string? Reason { get; set; }
    public List<PackageStatusDTO>? History { get; set; }
}
