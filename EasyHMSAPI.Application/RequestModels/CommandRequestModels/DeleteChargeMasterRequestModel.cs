using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteChargeMasterRequestModel : IRequest<DeleteChargeMasterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid ChargeId { get; set; }
    }
}
