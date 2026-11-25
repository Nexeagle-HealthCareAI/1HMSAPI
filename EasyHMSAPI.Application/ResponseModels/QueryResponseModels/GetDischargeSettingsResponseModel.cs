using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDischargeSettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public DischargeSettingsDataModel? Data { get; set; }
        /// <summary>The hospital's letterhead, returned to a doctor so printing can fall back to it
        /// when the doctor has none of their own. Null when the hospital has none either.</summary>
        public DischargeSettingsDataModel? HospitalDefault { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DischargeSettingsDataModel
    {
        public Guid? DischargeSettingId { get; set; }
        public Guid? HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public int HeaderHeight { get; set; }
        public int FooterHeight { get; set; }
        public int ContentLeftMargin { get; set; }
        public int ContentRightMargin { get; set; }
        public bool OverFlowPage { get; set; }
        public string? FontFamily { get; set; }
        public int FontSize { get; set; }
        public string? FontWeight { get; set; }
        public string? TextColour { get; set; }
        public string? URI { get; set; }
        public bool UseSystemDefaultLetterhead { get; set; }
        public bool IsHospitalLetterhead { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
