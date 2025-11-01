using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetAppointmentDepartmentsRequestModel : IRequest<GetAppointmentDepartmentsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
