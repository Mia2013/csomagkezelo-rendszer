namespace Csomagkuldo.DTOs;

public class DeliveryAssignmentResultDTO
{
    public List<PackageDTO> Assigned { get; set; } = new List<PackageDTO>();
    public List<PackageDTO> Unassigned { get; set; } = new List<PackageDTO>();
}
