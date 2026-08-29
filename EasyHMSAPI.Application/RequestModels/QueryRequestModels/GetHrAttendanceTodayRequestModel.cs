using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHrAttendanceTodayRequestModel : IRequest<GetHrAttendanceTodayResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateOnly Date { get; set; }
    }
}
