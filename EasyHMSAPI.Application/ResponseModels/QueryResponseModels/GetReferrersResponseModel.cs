using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetReferrersResponseModel
    {
        public List<ReferrerInfo> Referrers { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ReferrerInfo
    {
        public Guid ReferrerId { get; set; }
        public string ReferrerName { get; set; } = string.Empty;
        public string ReferrerType { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Pan { get; set; }
        public decimal DefaultRatePercent { get; set; }
        public bool IsActive { get; set; }
        public int ReferredPatientCount { get; set; }   // distinct patients referred by this referrer
    }
}
