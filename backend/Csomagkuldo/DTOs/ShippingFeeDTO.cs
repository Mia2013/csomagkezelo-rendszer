using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs
{
    public class ShippingFeeDTO
    {
        public int Id { get; set; }
        public PackageSize Type { get; set; }
        public string? MaxSize { get; set; }
        public int RegisteredPrice { get; set; }
        public int UnregisteredPrice { get; set; }
        public int CapacityUnit { get; set; }
    }
}
