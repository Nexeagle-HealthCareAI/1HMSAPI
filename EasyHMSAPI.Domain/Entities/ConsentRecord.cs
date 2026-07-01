using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("ConsentRecord")]
    public class ConsentRecord
    {
        [Key]
        public Guid ConsentRecordId { get; set; }

        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public Guid ConsentTemplateId { get; set; }
        public string TemplateTypeCode { get; set; } = null!;   // GENERAL_ADMISSION / PROCEDURE / ...
        public string? TemplateTitle { get; set; }
        public string? TemplateLanguage { get; set; }
        public int TemplateVersion { get; set; }
        public string? TemplateBodyHtmlSnapshot { get; set; }

        public string? ProcedureName { get; set; }

        public string SignedByName { get; set; } = null!;
        public string SignerRelation { get; set; } = null!;
        public string? SignerIdType { get; set; }
        public string? SignerIdNumber { get; set; }

        public string? SignatureImageBase64 { get; set; }

        public string? WitnessName { get; set; }
        public string? WitnessRole { get; set; }

        public DateTime SignedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
