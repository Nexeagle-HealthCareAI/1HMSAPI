using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Per-hospital override of PlatformSetting's FreeTierMonthlyLimit -- absent row means "use the
    // global default". CMS-editable; easyHMSAPI's UsageLimitService reads it.
    [ExcludeFromCodeCoverage]
    [Table("HospitalFreeTierLimit")]
    public class HospitalFreeTierLimit
    {
        [Key]
        public Guid HospitalId { get; set; }
        public int MonthlyLimit { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
