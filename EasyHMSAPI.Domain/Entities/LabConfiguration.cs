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

        // The lab's own identity for its report letterhead -- distinct from the hospital's generic
        // Name/Location-City-State-Pincode/RegistrationNumber (Hospital.cs), which a multi-service
        // facility's pathology lab may not want to reuse verbatim. Null/empty falls back to the
        // corresponding Hospital field at render time (see resolvePathologyBranding.ts) rather than
        // requiring every lab to re-type data the hospital profile already has.
        public string? LabName { get; set; }
        public string? LabAddress { get; set; }
        public string? LabRegistrationNumber { get; set; }

        // Printed as a static manual sign-off line at the bottom of every generated report -- not a
        // per-report workflow (see the dead Technician*/Pathologist*/ApprovedAt columns on
        // PathologyReport.cs, left from a removed sign-off pipeline this does NOT resurrect).
        // TechnicianName has no fallback anywhere in the system, so it's the one field the Pathology
        // workspace gates new-order creation on; PathologistName stays optional since many labs have
        // no in-house pathologist.
        public string? TechnicianName { get; set; }
        public string? PathologistName { get; set; }

        // When true, the report renderer leaves the configured top/bottom margin band blank
        // (physical pre-printed stationery already has the hospital's header/footer on it) instead
        // of drawing the digital letterhead there. Superseded by LetterheadMode below -- left as-is,
        // unused, rather than removed, since nothing else references it either.
        public bool IsPreprintedStationery { get; set; }

        // CUSTOM_TEMPLATE | BLANK_PREPRINTED | SYSTEM_DEFAULT -- which source the pathology report
        // PDF draws its header/footer from. A 3-state string rather than a bool: prescription and
        // discharge's single UseSystemDefaultLetterhead boolean can't distinguish "nothing
        // configured" from "deliberately left blank" from "deliberately default," so this doesn't
        // reuse that pattern. CUSTOM_TEMPLATE resolves via whichever PathologyReportTemplate has
        // IsDefault = true; it isn't a file reference itself.
        public string LetterheadMode { get; set; } = "SYSTEM_DEFAULT";

        // Hospital-wide report field layout: { "reportFields": [...], "lineFields": [...] }, each
        // an ordered PathologyFieldConfigItem list (see pathologyFieldLayoutApi.ts) -- reportFields
        // fill in once per report (Clinical History, Comments...), lineFields repeat on every test
        // line alongside the built-in Interpretation / Notes field. Null/empty means "use the
        // built-in defaults," merged client-side, same evolvable-JSON-blob trick as LetterheadMode.
        public string? ReportFieldLayoutJson { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
