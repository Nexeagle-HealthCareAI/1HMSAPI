using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
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