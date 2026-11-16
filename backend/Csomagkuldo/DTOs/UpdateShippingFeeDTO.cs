namespace Csomagkuldo.DTOs
{
    public class UpdateShippingFeeDTO
    {
        public string? MaxSize { get; set; }
        public int RegisteredPrice { get; set; }
        public int UnregisteredPrice { get; set; }
        public int CapacityUnit { get; set; }
    }
}
