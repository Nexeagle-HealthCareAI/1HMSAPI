using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Department stock request. Status: DRAFT/SUBMITTED/APPROVED/REJECTED/CONVERTED_TO_PO/CANCELLED.</summary>
    [ExcludeFromCodeCoverage]
    public class Indent
    {
        [Key]
        public Guid IndentId { get; set; }
        public Guid HospitalId { get; set; }

        public string IndentNumber { get; set; } = null!;
        public Guid RequestingStoreId { get; set; }
        public Guid? TargetStoreId { get; set; }

        public string Status { get; set; } = null!;
        public bool IsSystemGenerated { get; set; }

        public string? RequestedBy { get; set; }
        public Guid? RequestedByUserId { get; set; }
        public DateTime RequestedAt { get; set; }

        public string? ApprovedBy { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectedReason { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class IndentLine
    {
        [Key]
        public Guid IndentLineId { get; set; }
        public Guid IndentId { get; set; }
        public Guid InventoryItemId { get; set; }
        public decimal Qty { get; set; }
        public string? Notes { get; set; }
    }
}
