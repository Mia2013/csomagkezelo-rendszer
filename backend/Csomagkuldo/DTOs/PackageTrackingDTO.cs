using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs;

public class PackageTrackingDTO
{
    public string TrackingNumber { get; set; } = string.Empty;
    public PackageStatus Status { get; set; }
    public DateTime ChangedAt { get; set; }
}
