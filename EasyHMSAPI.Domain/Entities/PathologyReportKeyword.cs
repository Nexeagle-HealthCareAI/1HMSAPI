using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Hospital-scoped "type a keyword, get a formatted paragraph" template for pathology report
    // authoring (Interpretation / Notes and paragraph-type custom fields). TestId is a soft
    // reference (no FK, matching PathologyOrderLine.TestId's own convention in this module) -- null
    // means the keyword is usable while reporting on any test, not just one.
    // ContentJson is a StyledRun[] array (see richText.ts on the frontend) -- opaque to the
    // backend, same treatment as ParameterSchemaJson/ReportFieldLayoutJson elsewhere in this
    // module: parsed only on the client, never inspected here.
    [ExcludeFromCodeCoverage]
    public class PathologyReportKeyword
    {
        [Key]
        public Guid KeywordId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid? TestId { get; set; }

        public string Keyword { get; set; } = null!;
        public string ContentJson { get; set; } = null!;

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
