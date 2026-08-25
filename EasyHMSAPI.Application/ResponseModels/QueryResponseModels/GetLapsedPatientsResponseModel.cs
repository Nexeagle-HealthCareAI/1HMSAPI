using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetLapsedPatientsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public LapsedPatientsData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class LapsedPatientsData
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public string Outlook { get; set; } = string.Empty;
        public string SuggestedOutreachMessage { get; set; } = string.Empty;
        public List<LapsedPatientItem> Patients { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class LapsedPatientItem
    {
        public string PatientId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool MarketingConsent { get; set; }
        public int VisitCount { get; set; }
        public DateTime LastVisitDate { get; set; }
        public int DaysSinceLastVisit { get; set; }
        public decimal AverageGapDays { get; set; }
    }
}
