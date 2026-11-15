namespace Csomagkuldo.DTOs
{
    public class CourierCarDTO
    {
        public int Id { get; set; }
        public int Capacity { get; set; }
        public int? CourierId { get; set; }
        public string? CourierName { get; set; }
        public string? LicensePlate { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public int Year { get; set; }
    }
}
