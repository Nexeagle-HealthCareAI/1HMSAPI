using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // One row per (HospitalId, YearMonth) -- pooled count of free-tier "patient management
    // actions" (IPD admission, OPD appointment confirm/walk-in, pathology order, pharmacy
    // checkout) this hospital has used this calendar month. Written only via UsageLimitService's
    // atomic raw-SQL upsert (UPDLOCK/HOLDLOCK) -- never through plain EF Add/SaveChanges, so no
    // navigation property or [Key] here is load-bearing for writes.
    [ExcludeFromCodeCoverage]
    [Table("HospitalMonthlyUsage")]
    public class HospitalMonthlyUsage
    {
        public Guid HospitalId { get; set; }
        public string YearMonth { get; set; } = null!;
        public int UsedCount { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
