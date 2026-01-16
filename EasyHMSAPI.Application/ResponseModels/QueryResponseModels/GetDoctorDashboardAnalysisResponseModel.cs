using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDoctorDashboardAnalysisResponseModel
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public DashboardAnalysisData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DashboardAnalysisData
    {
        public KPIData? KPI { get; set; }
        public MedicalStatsData? MedicalStats { get; set; }
        public BPStatsData? BPStats { get; set; }
        public WeightStatsData? WeightStats { get; set; }
        public BMIStatsData? BMIStats { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class KPIData
    {
        public VisitData? TotalVisits { get; set; }
        public VisitData? UniquePatients { get; set; }
        public PatientTypeData? NewVsReturningPatients { get; set; }
        public Dictionary<string, int>? AgeDistribution { get; set; }
        public int NoShow { get; set; }
        public int Cancelled { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class VisitData
    {
        public int Overall { get; set; }
        public TimeBucketData? ByBucket { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TimeBucketData
    {
        public int Today { get; set; }
        public int Yesterday { get; set; }
        public int Last7Days { get; set; }
        public int ThisMonth { get; set; }
        public int ThisYear { get; set; }
        public int PrevYear { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientTypeData
    {
        public PatientCountData? New { get; set; }
        public PatientCountData? Returning { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PatientCountData
    {
        public int Count { get; set; }
        public decimal Percent { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MedicalStatsData
    {
        public Dictionary<string, int>? Top5MedicineUse { get; set; }
        public Dictionary<string, int>? Top5Complain { get; set; }
        public Dictionary<string, int>? Top5Diagnosis { get; set; }
        public Dictionary<string, int>? Top5Investigation { get; set; }
        public Dictionary<string, int>? Top5Examination { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BPStatsData
    {
        public Dictionary<string, int>? CategoryCounts { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class WeightStatsData
    {
        public List<WeightBucketData>? Buckets { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class WeightBucketData
    {
        public string? Range { get; set; }
        public int Count { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BMIStatsData
    {
        public Dictionary<string, int>? CategoryCounts { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class VitalData
    {
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public decimal? Weight { get; set; }
        public decimal? BMI { get; set; }
    }
}
