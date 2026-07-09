using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class AdmissionReferralStatusHistory
    {
        public Guid HistoryId { get; set; }
        public Guid ReferralId { get; set; }
        public string StatusCode { get; set; } = null!;
        public DateTime ChangedAt { get; set; }
        public string? ChangedBy { get; set; }
        public string? Notes { get; set; }
    }
}
