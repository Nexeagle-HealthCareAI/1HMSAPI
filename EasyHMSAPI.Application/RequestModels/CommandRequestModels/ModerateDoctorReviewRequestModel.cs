using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class ModerateDoctorReviewRequestModel : IRequest<ModerateDoctorReviewResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid ReviewId { get; set; }
        public bool IsHidden { get; set; }
    }
}
