using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Payer / policy / scheme detail for an admission. Populated for TPA and SCHEME payers;
    /// CASH admissions typically have none. Gives pre-auth / enhancement (later phases) a home.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AdmissionCoverage")]
    public class AdmissionCoverage
    {
        [Key]
        public Guid CoverageId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }

        public string PayerType { get; set; } = null!;   // CASH / TPA / SCHEME
        public string? PayerName { get; set; }           // insurer / TPA / scheme name
        public string? PolicyOrBeneficiaryNo { get; set; }
        public string? PreAuthNo { get; set; }
        public string? PackageCode { get; set; }         // PM-JAY HBP package code
        public decimal? SanctionedAmount { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        public string StatusCode { get; set; } = "PENDING";   // PENDING/APPROVED/QUERIED/REJECTED/ENHANCED
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
