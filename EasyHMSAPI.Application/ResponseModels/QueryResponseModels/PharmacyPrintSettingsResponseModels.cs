using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPharmacyPrintSettingsResponseModel
    {
        public bool Configured { get; set; }
        public string? TradeName { get; set; }
        public string? Dl20BNumber { get; set; }
        public string? Dl21BNumber { get; set; }
        public string? FssaiNumber { get; set; }
        public string? PharmacistName { get; set; }
        public string? PharmacistRegNo { get; set; }
        public string? ReturnPolicyText { get; set; }
        public bool ShowVerificationQr { get; set; } = true;
        // Convenience passthrough so the frontend doesn't need a second call to get GSTIN.
        public string? HospitalGstin { get; set; }
    }
}
