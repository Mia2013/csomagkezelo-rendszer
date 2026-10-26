using System.ComponentModel.DataAnnotations;

namespace Csomagkuldo.Models;

public class CourierCar
{
    public int Id { get; set; }

    [ConcurrencyCheck]
    public int Capacity { get; set; }

    [ConcurrencyCheck]
    public int? CourierId { get; set; }

    public string LicensePlate { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }

    public User? Courier { get; set; }
}
