using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalOverallAnalysisResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public HospitalAnalysisDataModel? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class HospitalAnalysisDataModel
    {
        public KpisModel? Kpis { get; set; }
        public BreakdownsModel? Breakdowns { get; set; }
        public OverallModel? Overall { get; set; }
        public List<GenderWiseModel>? GenderWise { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class KpisModel
    {
        public VisitMetricModel? TotalVisits { get; set; }
        public VisitMetricModel? UniquePatients { get; set; }
        public PatientTypeModel? NewVsReturningPatients { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class VisitMetricModel
    {
        public int Overall { get; set; }
        public BucketMetricModel? ByBucket { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BucketMetricModel
    {
        public int Today { get; set; }
        public int Yesterday { get; set; }
        public int Last7Days { get; set; }
        public int ThisMonth { get; set; }
        public int ThisYear { get; set; }
        public int PrevYear { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientTypeModel
    {
        public PatientCountModel? New { get; set; }
        public PatientCountModel? Returning { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientCountModel
    {
        public int Count { get; set; }
        public decimal Percent { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BreakdownsModel
    {
        public List<DoctorBreakdownModel>? ByDoctor { get; set; }
        public List<SpecialtyBreakdownModel>? BySpecialty { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DoctorBreakdownModel
    {
        public Guid DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public string? Specialty { get; set; }
        public int OverallVisits { get; set; }
        public int UniquePatients { get; set; }
        public NewPatientMetricModel? NewPatients { get; set; }
        public int ReturningPatients { get; set; }
        public int FirstVisits { get; set; }
        public int NoShow { get; set; }
        public decimal SharePercent { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class NewPatientMetricModel
    {
        public int Day { get; set; }
        public int Week { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SpecialtyBreakdownModel
    {
        public string? SpecialtyCode { get; set; }
        public string? SpecialtyName { get; set; }
        public int OverallVisits { get; set; }
        public int UniquePatients { get; set; }
        public decimal SharePercent { get; set; }
        public TrendModel? TrendVsPreviousPeriod { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TrendModel
    {
        public decimal Percent { get; set; }
        public string? Direction { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class OverallModel
    {
        public Dictionary<string, int>? AgeDistribution { get; set; }
        public int NoShow { get; set; }
        public int Cancelled { get; set; }
        public Dictionary<string, int>? Top5City { get; set; }
        public List<string>? UniqueCities { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GenderWiseModel
    {
        public string? Gender { get; set; }
        public int OverallVisits { get; set; }
        public int NoShow { get; set; }
        public int Cancelled { get; set; }
        public Dictionary<string, int>? AgeDistribution { get; set; }
    }
}
