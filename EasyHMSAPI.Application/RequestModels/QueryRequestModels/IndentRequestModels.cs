using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetIndentsRequestModel : IRequest<GetIndentsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetIndentDetailRequestModel : IRequest<GetIndentDetailResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid IndentId { get; set; }
    }
}
