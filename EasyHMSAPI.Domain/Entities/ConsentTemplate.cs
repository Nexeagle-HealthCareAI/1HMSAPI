using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>A versioned consent form body. New versions supersede the prior active one for
    /// the same (HospitalId, TypeCode, Language) — see ConsentTemplateCommandHandlers.</summary>
    [ExcludeFromCodeCoverage]
    [Table("ConsentTemplate")]
    public class ConsentTemplate
    {
        [Key]
        public Guid ConsentTemplateId { get; set; }
        public Guid HospitalId { get; set; }

        public string TypeCode { get; set; } = null!;   // GENERAL_ADMISSION / PROCEDURE / ...
        public string? Title { get; set; }
        public string? Language { get; set; }
        public int Version { get; set; } = 1;
        public string? BodyHtml { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
