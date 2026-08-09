using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetNursingStationSummaryResponseModel
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public string? NurseName { get; set; }

        // False = this nurse has no active roster row at all (distinct from "rostered but the
        // ward is empty right now") -- the UI must render these two states differently.
        public bool HasAssignments { get; set; }

        public int TotalPatients { get; set; }
        public int TotalMedsDue { get; set; }
        public int TotalMedsOverdue { get; set; }

        public List<NursingStationPatientItem> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class NursingStationPatientItem
    {
        public Guid AdmissionId { get; set; }
        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public short? PatientAge { get; set; }
        public string? PatientSex { get; set; }
        public string? BedCode { get; set; }
        public string WardCode { get; set; } = null!;
        public string? WardName { get; set; }
        public string? PrimaryDoctorName { get; set; }

        // Raw timestamp/values, not pre-formatted -- the frontend already has IST-formatting utils.
        public DateTime? LastVitalAt { get; set; }
        public int? LastPulse { get; set; }
        public int? LastSystolicBP { get; set; }
        public int? LastDiastolicBP { get; set; }
        public decimal? LastTemperature { get; set; }
        public decimal? LastSpO2 { get; set; }

        public int MedsDueCount { get; set; }
        public int MedsOverdueCount { get; set; }
        public DateTime? NextDoseAtUtc { get; set; }

        // Per-patient assignment (PatientNurseAssignment), independent of the ward roster this
        // board is otherwise driven by. Empty when nobody has been specifically assigned yet.
        public List<string> AssignedNurseNames { get; set; } = new();
    }
}
