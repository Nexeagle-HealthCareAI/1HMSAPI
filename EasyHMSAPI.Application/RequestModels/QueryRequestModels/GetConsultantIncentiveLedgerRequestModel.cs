using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Per-doctor summary (accrued/paid/cancelled totals) across all doctors with any ledger
    // activity — the landing view for the consultant ledger screen.
    [ExcludeFromCodeCoverage]
    public class GetConsultantIncentiveSummaryRequestModel : IRequest<GetConsultantIncentiveSummaryResponseModel>
    {
        public Guid HospitalId { get; set; }
    }

    // Line-level drill-in for one doctor, optionally filtered by status/date range.
    [ExcludeFromCodeCoverage]
    public class GetConsultantIncentiveLedgerRequestModel : IRequest<GetConsultantIncentiveLedgerResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? StatusCode { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
