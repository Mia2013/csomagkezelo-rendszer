using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs;

public class CourierTaskDTO
{
    public int Id { get; set; }
    public int CourierId { get; set; }
    public int PackageId { get; set; }
    public CourierTaskType TaskType { get; set; }
    public CourierTaskStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public DateTime AssignmentTime { get; set; }
    public PackageDTO Package { get; set; } = new PackageDTO();
}
