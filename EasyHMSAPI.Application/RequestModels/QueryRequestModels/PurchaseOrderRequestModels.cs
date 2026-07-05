using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPurchaseOrdersRequestModel : IRequest<GetPurchaseOrdersResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPurchaseOrderDetailRequestModel : IRequest<GetPurchaseOrderDetailResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid PurchaseOrderId { get; set; }
    }
}
