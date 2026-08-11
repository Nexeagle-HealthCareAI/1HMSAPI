using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalLeadsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<HospitalLeadInfo> Leads { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        // Breakdown counts respect the DateFrom/DateTo window but NOT the Source/LeadType
        // filters (those ARE the breakdown) -- so the KPI cards stay meaningful even once the
        // table itself is filtered down to one source or type.
        public Dictionary<string, int> CountBySource { get; set; } = new();
        public Dictionary<string, int> CountByType { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class HospitalLeadInfo
    {
        public Guid LeadId { get; set; }
        public Guid? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public string Source { get; set; } = string.Empty;
        public string LeadType { get; set; } = string.Empty;
        public string? SearchQuery { get; set; }
        public string? Mobile { get; set; }
        public string? PatientName { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
