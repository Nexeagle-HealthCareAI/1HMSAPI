using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class DoctorFee
    {
        [Key]
        public Guid DoctorFeeId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string FeeType { get; set; } = string.Empty; // OPD_CONSULT / IPD_VISIT
        public decimal Amount { get; set; }
        public bool IsActive { get; set; } = true;
        // OPD_CONSULT only. Days after a paid visit that a follow-up stays free. 0 = no free
        // window at all, every visit is chargeable (opposite polarity of PrescriptionSetting's
        // ValidDuration, which treats 0 as "never expires").
        public int FreeFollowUpDays { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
