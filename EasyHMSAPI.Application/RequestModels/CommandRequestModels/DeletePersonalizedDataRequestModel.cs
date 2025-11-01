using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DeletePersonalizedDataRequestModel : IRequest<DeletePersonalizedDataResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public Guid PersonalId { get; set; }
    }
}
