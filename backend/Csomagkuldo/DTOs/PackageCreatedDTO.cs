using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs;

public class PackageCreatedDTO
{
    public string TrackingNumber { get; set; } = string.Empty;
    public PackageStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
