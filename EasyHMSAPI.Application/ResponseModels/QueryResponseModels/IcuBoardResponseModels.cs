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
        public string? PatientId { get; set; }
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

        // Every nurse currently rostered to this patient's ward (ward-level grain, kept as a
        // fallback for wards with no per-patient assignment yet). Empty when nobody is rostered.
        public List<string> NurseNames { get; set; } = new();

        // Real per-patient assignment (PatientNurseAssignment), independent of the ward roster
        // above. Prefer this over NurseNames when non-empty -- it's the actual assigned nurse(s),
        // not just "someone on this ward."
        public List<string> AssignedNurseNames { get; set; } = new();

        // Raw values, not pre-formatted -- the frontend already has its own age/staleness formatting.
        public DateTime? LastVitalAt { get; set; }
        public int? LastPulse { get; set; }
        public int? LastSystolicBP { get; set; }
        public int? LastDiastolicBP { get; set; }
        public decimal? LastTemperature { get; set; }
        public decimal? LastSpO2 { get; set; }
    }
}
