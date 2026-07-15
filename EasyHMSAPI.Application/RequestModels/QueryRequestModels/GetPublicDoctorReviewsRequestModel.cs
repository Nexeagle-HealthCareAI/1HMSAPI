using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorReviewsRequestModel : IRequest<GetPublicDoctorReviewsResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
