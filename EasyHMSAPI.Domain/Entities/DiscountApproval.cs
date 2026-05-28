using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("DiscountApproval")]
    public class DiscountApproval
    {
        [Key]
        public Guid DiscountApprovalId { get; set; }
        public Guid HospitalId { get; set; }

        public Guid ChargeEventId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }

        public decimal GrossAmount { get; set; }
        public decimal RequestedDiscountPercent { get; set; }
        public decimal RequestedDiscountAmount { get; set; }

        public decimal CapPercent { get; set; }
        public decimal OverByPercent { get; set; }

        public string? Reason { get; set; }
        public string? RequestedBy { get; set; }
        public Guid? RequestedByUserId { get; set; }
        public DateTime RequestedAt { get; set; }

        public string Status { get; set; } = "PENDING";

        public DateTime? DecidedAt { get; set; }
        public string? DecidedBy { get; set; }
        public Guid? DecidedByUserId { get; set; }
        public string? DecisionNote { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
