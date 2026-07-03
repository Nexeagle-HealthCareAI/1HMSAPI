using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetConsultantIncentiveSummaryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ConsultantIncentiveDoctorSummary>? Doctors { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ConsultantIncentiveDoctorSummary
    {
        public Guid DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public decimal AccruedTotal { get; set; }
        public decimal PaidTotal { get; set; }
        public decimal CancelledTotal { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetConsultantIncentiveLedgerResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ConsultantIncentiveLineModel>? Lines { get; set; }
        public decimal AccruedTotal { get; set; }
        public decimal PaidTotal { get; set; }
        public decimal CancelledTotal { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ConsultantIncentiveLineModel
    {
        public Guid ConsultantIncentiveLedgerId { get; set; }
        public string? PatientId { get; set; }
        public Guid ChargeEventId { get; set; }
        public string? ChargeDisplayName { get; set; }
        public decimal IncentiveAmount { get; set; }
        public string? StatusCode { get; set; }
        public DateTime AccruedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? PayoutRef { get; set; }
        public decimal? TdsAmount { get; set; }
    }
}
