using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Values of custom discharge-summary fields for one admission, as a JSON object
    /// (field key -> text). Table: easyHMSDatabase create_tables_discharge_summary_custom_fields.sql.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("DischargeSummaryCustomField")]
    public class DischargeSummaryCustomField
    {
        [Key]
        public Guid AdmissionId { get; set; }
        public Guid HospitalId { get; set; }
        public string FieldsJson { get; set; } = "{}";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
    }
}
