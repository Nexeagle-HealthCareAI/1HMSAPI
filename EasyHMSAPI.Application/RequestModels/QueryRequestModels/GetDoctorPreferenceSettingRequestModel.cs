using MediatR;
using System;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetDoctorPreferenceSettingRequestModel : IRequest<GetDoctorPreferenceSettingResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}