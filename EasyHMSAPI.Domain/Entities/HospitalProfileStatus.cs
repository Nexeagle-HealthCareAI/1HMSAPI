using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class HospitalProfileStatus
    {
        [Key]
        public Guid HospitalID { get; set; }
        public bool IsBasicInfoComplete { get; set; } = false;
        public bool IsContactInfoComplete { get; set; } = false;
        public bool IsLocationInfoComplete { get; set; } = false;
        public int ProfileCompletionPercent { get; set; } = 0;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        public Hospital Hospital { get; set; } = null!;
    }
}