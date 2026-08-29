using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetAttendanceExceptionsRequestModel : IRequest<GetAttendanceExceptionsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
