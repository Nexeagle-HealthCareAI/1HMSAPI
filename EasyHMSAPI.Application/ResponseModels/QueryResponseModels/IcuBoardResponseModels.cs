using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetIcuBoardResponseModel
    {
        public List<IcuBoardCaseDataModel> Cases { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class IcuBoardCaseDataModel
    {
        public Guid AdmissionId { get; set; }
        public Guid EncounterId { get; set; }
        public string? PatientName { get; set; }
        public string? BedCode { get; set; }
        public string? WardCode { get; set; }
        public string? IcuLevel { get; set; } // LEVEL_1, LEVEL_2, LEVEL_3
        public decimal? ApacheScore { get; set; }
        public decimal? SofaScore { get; set; }
        public bool OnVentilator { get; set; }
        public string? PrimaryDiagnosis { get; set; }
        public int? EwsScore { get; set; }
        public string? EwsRiskBand { get; set; }
        public bool HasOpenRapidResponse { get; set; }
        public int ActiveDeviceCount { get; set; }
        public bool HasOverdueBundleCheck { get; set; }
    }
}
