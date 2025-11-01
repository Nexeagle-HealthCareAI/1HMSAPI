using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyHMSAPI.Domain.Entities
{
    public class PrescriptionHeaderFooter
    {
        [Key]
        public Guid PrescriptionTemplateID { get; set; }
        public Guid HospitalID { get; set; }
        public string? HeaderHTML { get; set; }
        public string? FooterHTML { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}