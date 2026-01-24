using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteBillingChargesRequestModel : IRequest<DeleteBillingChargesResponseModel>
    {
        public Guid ChargeItemId { get; set; }
        public Guid HospitalId { get; set; }
    }
}
