using System.ComponentModel.DataAnnotations;

namespace Csomagkuldo.DTOs;

public class CourierCarDTO
{
    public int? Id { get; set; }
    [Range(1, int.MaxValue)]
    public int? Capacity { get; set; }
    [Range(1, int.MaxValue)]
    public int? CourierId { get; set; }
    [StringLength(20)]
    public string? LicensePlate { get; set; }
    [StringLength(100)]
    public string? Manufacturer { get; set; }
    [StringLength(100)]
    public string? Model { get; set; }
    [Range(1, 9999)]
    public int? Year { get; set; }

    // Ezeket a kapacitáslekérdezés tölti ki, mentéskor nem vesszük át őket.
    public bool? IsActive { get; set; }
    public int? UsedCapacity { get; set; }
    public int? FreeCapacity { get; set; }
}
