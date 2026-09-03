using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Pharmacy-specific statutory/print fields (Drug License numbers, FSSAI, registered pharmacist,
    /// return policy) — kept separate from the generic InvoicePrintSettings (font/margin config for
    /// the general hospital invoice) since pharmacy bills carry Drugs &amp; Cosmetics Act-mandated
    /// fields no other bill type needs. One row per hospital.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PharmacyPrintSettings
    {
        [Key]
        public Guid PharmacyPrintSettingsId { get; set; }
        public Guid HospitalId { get; set; }

        public string? TradeName { get; set; }
        public string? Dl20BNumber { get; set; }
        public string? Dl21BNumber { get; set; }
        public string? FssaiNumber { get; set; }
        public string? PharmacistName { get; set; }
        public string? PharmacistRegNo { get; set; }
        public string? ReturnPolicyText { get; set; }
        public bool ShowVerificationQr { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
