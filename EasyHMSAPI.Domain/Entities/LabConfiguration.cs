using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class LabConfiguration
    {
        [Key]
        public Guid ConfigId { get; set; }
        public Guid HospitalId { get; set; }
        
        public bool AutoBillOnOrder { get; set; }

        public string? DefaultReportHeaderBlob { get; set; }
        public string? DefaultReportFooterText { get; set; }

        // Accreditation badge shown on generated report letterheads. Null/empty fields simply
        // don't render their badge line -- no accreditation is a valid, common state for a
        // Tier 3/4 facility, not an error.
        public string? NablAccreditationNumber { get; set; }
        public string? NablLogoUrl { get; set; }
        public string? Iso15189Number { get; set; }
        public string? IcmrRegistrationId { get; set; }

        // When true, the report renderer leaves the configured top/bottom margin band blank
        // (physical pre-printed stationery already has the hospital's header/footer on it) instead
        // of drawing the digital letterhead there.
        public bool IsPreprintedStationery { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
