using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetConsultantFeeConfigRequestModel : IRequest<GetConsultantFeeConfigResponseModel>
    {
        public Guid HrEmployeeId { get; set; }
    }
}
