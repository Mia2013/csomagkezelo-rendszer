namespace Csomagkuldo.DTOs
{
    public class CreateUpdateCarDTO
    {
        public int Capacity { get; set; }
        public int? CourierId { get; set; }
        public string? LicensePlate { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public int Year { get; set; }
    }
}
