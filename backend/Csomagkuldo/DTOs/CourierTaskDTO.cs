using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs;

public class CourierTaskDTO
{
    public int Id { get; set; }
    public int PackageId { get; set; }
    public CourierTaskType TaskType { get; set; }
    public CourierTaskStatus Status { get; set; }
    public DateTime AssignmentTime { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public PackageSize Size { get; set; }
    public DateTime PickupDate { get; set; }
    public string? PickupSlot { get; set; }
}
