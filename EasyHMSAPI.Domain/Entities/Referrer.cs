using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Referrer
    {
        [Key]
        public Guid ReferrerId { get; set; }
        public Guid HospitalId { get; set; }
        public string ReferrerName { get; set; } = string.Empty;
        public string ReferrerType { get; set; } = "REFERRER"; // REFERRER/DOCTOR/STAFF/AGENT/DEPARTMENT
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Pan { get; set; }
        public decimal DefaultRatePercent { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
