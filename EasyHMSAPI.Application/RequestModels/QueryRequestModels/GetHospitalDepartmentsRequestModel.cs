using MediatR;
using System;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHospitalDepartmentsRequestModel : IRequest<GetHospitalDepartmentsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
