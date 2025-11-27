using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateDoctorPreferenceSettingRequestModel : IRequest<UpdateDoctorPreferenceSettingResponseModel>
    {
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; } // Added hospitalId
        public DoctorSectionPreferenceUpdateModel? Preference { get; set; }
    }
}