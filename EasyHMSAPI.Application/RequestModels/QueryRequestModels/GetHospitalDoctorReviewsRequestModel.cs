using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Admin moderation list — bound HospitalId triggers HospitalAccessFilter's automatic
    // caller-is-a-member-of-this-hospital check, same convention as the doctor-tile edit path.
    [ExcludeFromCodeCoverage]
    public class GetHospitalDoctorReviewsRequestModel : IRequest<GetHospitalDoctorReviewsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
    }
}
