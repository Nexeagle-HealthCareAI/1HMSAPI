using System;
using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetPrescriptionSettingsRequestModel : IRequest<GetPrescriptionSettingsResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
