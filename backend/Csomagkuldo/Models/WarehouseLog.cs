namespace Csomagkuldo.Models
{
    public class WarehouseLog
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public int PackageCount { get; set; }

        public int? UsedCapacity { get; set; }

        public int? Capacity { get; set; }
    }
}
