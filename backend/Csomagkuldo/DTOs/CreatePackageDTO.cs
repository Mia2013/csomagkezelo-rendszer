using System.ComponentModel.DataAnnotations;

namespace Csomagkuldo.DTOs;

public class CreatePackageDTO
{
    [Required, StringLength(150)]
    public string SenderName { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string SenderAddress { get; set; } = string.Empty;

    [Required, StringLength(30), Phone]
    public string SenderPhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string RecipientName { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string RecipientAddress { get; set; } = string.Empty;

    [Required, StringLength(30), Phone]
    public string RecipientPhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(254), EmailAddress]
    public string RecipientEmail { get; set; } = string.Empty;

    [Required, Range(0, int.MaxValue)]
    public int? Value { get; set; }

    // A tömeget grammban kérjük, később lehet tört kiló is.
    [Required, Range(1, 30000)]
    public int? Weight { get; set; }

    [Required, RegularExpression("^(S|M|L)$")]
    public string Size { get; set; } = string.Empty;

    [Required]
    public DateOnly? PickupDate { get; set; }

    [Required, RegularExpression("^(09:00-12:00|12:00-16:00)$")]
    public string PickupSlot { get; set; } = string.Empty;
}
