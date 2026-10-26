namespace Csomagkuldo.Models
{
    public class WarehouseLog
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public int PackageCount { get; set; }

        // Régi naplóknál a helyfoglalás és a teljes kapacitás még nem volt ismert.
        public int? UsedCapacity { get; set; }

        public int? Capacity { get; set; }
    }
}
