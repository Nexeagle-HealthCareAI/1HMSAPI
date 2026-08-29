using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHrHospitalShiftsRequestModel : IRequest<GetHrHospitalShiftsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
