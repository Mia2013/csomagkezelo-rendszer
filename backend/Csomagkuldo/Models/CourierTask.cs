using Csomagkuldo.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Csomagkuldo.Models
{
    public class CourierTask
    {
        public int Id { get; set; }

        [ConcurrencyCheck]
        public int CourierId { get; set; }

        public int PackageId { get; set; }

        public CourierTaskType TaskType { get; set; }

        public DateTime AssignmentTime { get; set; }
            = DateTime.UtcNow;

        [ConcurrencyCheck]
        public CourierTaskStatus Status { get; set; }
            = CourierTaskStatus.Assigned;

        // Megkezdett feladatnál a raktáros már nem változtathat futárt.
        [ConcurrencyCheck]
        public bool IsLocked { get; set; }

        [ForeignKey("CourierId")]
        [InverseProperty("CourierTasks")]
        public User Courier { get; set; } = null!;

        [ForeignKey("PackageId")]
        [InverseProperty("CourierTasks")]
        public Package Package { get; set; } = null!;
    }
}
