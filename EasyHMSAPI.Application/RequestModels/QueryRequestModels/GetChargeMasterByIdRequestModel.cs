using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetChargeMasterByIdRequestModel : IRequest<GetChargeMasterByIdResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid ChargeId { get; set; }
    }
}
