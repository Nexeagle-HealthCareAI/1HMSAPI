using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalBillingDashboardRequestModel : IRequest<GetHospitalBillingDashboardResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
