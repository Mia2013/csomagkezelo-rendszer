using Csomagkuldo_Backend.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Csomagkuldo_Backend.Models
{
    public class PackageStatusHistory
    {
        public int Id { get; set; }

        public int PackageId { get; set; }

        public int? CourierId { get; set; }

        public PackageStatus Status { get; set; }

        public DateTime ChangedAt { get; set; }
            = DateTime.UtcNow;

        [ForeignKey("PackageId")]
        [InverseProperty("StatusHistory")]
        public Package Package { get; set; } = null!;

        [ForeignKey("CourierId")]
        [InverseProperty("PackageStatusChanges")]
        public User? Courier { get; set; }
    }
}
