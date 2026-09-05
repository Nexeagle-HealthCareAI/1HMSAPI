using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Global key-value settings, CMS-editable. Shared physical table with CMSAPI's own mapping of
    // the same dbo.PlatformSetting rows (both apps' DbContexts point at the same easyHMSDatabase
    // catalog) -- CMS writes FreeTierMonthlyLimit here, easyHMSAPI's UsageLimitService reads it.
    [ExcludeFromCodeCoverage]
    [Table("PlatformSetting")]
    public class PlatformSetting
    {
        [Key]
        public string SettingKey { get; set; } = null!;
        public string SettingValue { get; set; } = null!;
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
