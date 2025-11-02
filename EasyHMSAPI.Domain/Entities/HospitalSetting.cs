using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class HospitalSetting
    {
        [Key]
        public Guid HospitalID { get; set; }
        public string? BrandingConfig { get; set; }
        public DateTime? LastUpdated { get; set; }
        public Hospital? Hospital { get; set; }
    }
}
