using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs;

public class PackageStatusDTO
{
    public PackageStatus Status { get; set; }
    public DateTime ChangedAt { get; set; }
}
