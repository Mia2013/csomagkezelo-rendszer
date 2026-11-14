using Csomagkuldo.Enums;

namespace Csomagkuldo.DTOs
{
    public class UpdatePackageDTO
    {
        public string? PickupSlot { get; set; }
        public string? RecipientAddress { get; set; }
        public PackageSize? Size { get; set; }
    }
}
