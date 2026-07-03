using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Per-payer rate override for one ChargeMaster item. Absence of a row for a (ChargeId, PayerType) pair falls through to ChargeMaster.DefaultRate.</summary>
    [ExcludeFromCodeCoverage]
    [Table("ChargeMasterPayerRate")]
    public class ChargeMasterPayerRate
    {
        [Key]
        public Guid ChargeMasterPayerRateId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid ChargeId { get; set; }
        public string PayerType { get; set; } = null!;   // CASH/TPA/SCHEME
        public decimal OverrideRate { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
