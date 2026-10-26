using Csomagkuldo.Enums;

namespace Csomagkuldo.Models;

public class ShippingFee
{
    public int Id { get; set; }
    public ShippingFeeType Type { get; set; }
    public string MaxSize { get; set; } = string.Empty;
    public int RegisteredPrice { get; set; }
    public int UnregisteredPrice { get; set; }

    public List<Package> Packages { get; set; } = new List<Package>();
}
