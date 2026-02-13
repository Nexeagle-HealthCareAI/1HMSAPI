using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPrescriptionSettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public PriscriptionSettingsDataModel? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PriscriptionSettingsDataModel
    {
        public Guid? PrescriptionSettingsId { get; set; }
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
        public int ValidUpto{ get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
