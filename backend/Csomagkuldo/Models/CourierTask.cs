using Csomagkuldo.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Csomagkuldo.Models
{
    public class CourierTask
    {
        public int Id { get; set; }

        public int CourierId { get; set; }

        public int PackageId { get; set; }

        public CourierTaskType TaskType { get; set; }

        public DateTime AssignmentTime { get; set; }
            = DateTime.UtcNow;

        public CourierTaskStatus Status { get; set; }
            = CourierTaskStatus.Assigned;

        [ForeignKey("CourierId")]
        [InverseProperty("CourierTasks")]
        public User Courier { get; set; } = null!;

        [ForeignKey("PackageId")]
        [InverseProperty("CourierTasks")]
        public Package Package { get; set; } = null!;
    }
}
