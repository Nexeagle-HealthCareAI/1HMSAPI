using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("BloodBag")]
    public class BloodBag
    {
        [Key]
        public Guid BloodBagId { get; set; }
        public Guid HospitalId { get; set; }

        public string BagNumber { get; set; } = null!;
        public string Component { get; set; } = null!;    // WHOLE/PRBC/FFP/PLATELET/CRYO
        public string BloodGroup { get; set; } = null!;    // A_POS/A_NEG/B_POS/B_NEG/O_POS/O_NEG/AB_POS/AB_NEG
        public decimal VolumeMl { get; set; }
        public string? DonorRef { get; set; }
        public DateTime CollectedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? StorageLocation { get; set; }
        // Unified Store reference (INV-10) — added alongside StorageLocation, not replacing it.
        public Guid? StoreId { get; set; }

        public string Status { get; set; } = null!;   // AVAILABLE/RESERVED/TRANSFUSED/DISCARDED

        public Guid? ReservedForAdmissionId { get; set; }
        public Guid? ReservedForEncounterId { get; set; }
        public string? ReservedForPatientId { get; set; }
        public string? CrossmatchResult { get; set; }   // COMPATIBLE/INCOMPATIBLE/NOT_DONE
        public string? CrossmatchBy { get; set; }
        public DateTime? ReservedAt { get; set; }
        public string? ReservedBy { get; set; }

        public DateTime? DiscardedAt { get; set; }
        public string? DiscardedBy { get; set; }
        public string? DiscardReason { get; set; }

        public Guid? ChargeId { get; set; }
        public decimal? UnitRate { get; set; }
        public string? HsnSacCode { get; set; }
        public decimal? GstSlabPercent { get; set; }
        public bool IsTaxable { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
