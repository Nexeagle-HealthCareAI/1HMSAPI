using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class HospitalSetting
    {
        [Key]
        public Guid HospitalID { get; set; }
        public string? BrandingConfig { get; set; }
        public DateTime? LastUpdated { get; set; }

        public Hospital? Hospital { get; set; }
    }
}
