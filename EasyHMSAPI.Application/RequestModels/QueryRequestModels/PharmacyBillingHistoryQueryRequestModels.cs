using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Raw per-invoice pharmacy sales list — distinct from PharmacyAnalyticsQueryRequestModels'
    // aggregations. FromDate/ToDate are both optional: omit either (or both) for an unbounded
    // "all time" list — the frontend's "All" filter mode maps to sending neither.
    [ExcludeFromCodeCoverage]
    public class GetPharmacyBillingHistoryRequestModel : IRequest<GetPharmacyBillingHistoryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Optional -- omit both for the pre-pagination behavior (first 50 returned, same default).
        // Without any cap, the "All Time" filter mode pulled the hospital's entire pharmacy sales
        // history into memory and over the wire on every load.
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
