using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class ResetPrescriptionSettingsRequestModel : IRequest<ResetPrescriptionSettingsResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
