using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Insert-only audit trail — mirrors InventoryMovement. SurgeryCaseId is set on ISSUE_TO_OT/RETURN.</summary>
    [ExcludeFromCodeCoverage]
    [Table("InstrumentSetMovement")]
    public class InstrumentSetMovement
    {
        [Key]
        public Guid InstrumentSetMovementId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid InstrumentSetId { get; set; }

        public string MovementType { get; set; } = null!;   // ISSUE_TO_OT/RETURN/SEND_TO_WASH/PACK/QUARANTINE/DISCARD/RECEIVE_STERILE
        public Guid? SurgeryCaseId { get; set; }

        public DateTime MovedAt { get; set; }
        public string? MovedBy { get; set; }
        public Guid? MovedByUserId { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
