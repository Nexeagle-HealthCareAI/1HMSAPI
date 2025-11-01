using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class UpdateDoctorPreferenceSettingRequestModel : IRequest<UpdateDoctorPreferenceSettingResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DoctorSectionPreferenceUpdateModel? Preference { get; set; }
    }
}