using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingCategoryAnalyticsRequestModel : IRequest<GetBillingCategoryAnalyticsResponseModel>
    {
        public Guid HospitalId { get; set; }

        // Both null = all-time. Equal (same calendar day) = a single day. EndDate is inclusive of
        // its whole calendar day regardless of any time component.
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
