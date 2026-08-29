using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHrDutyRostersRequestModel : IRequest<GetHrDutyRostersResponseModel>
    {
        public Guid HospitalId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Guid LoggedInUserId { get; set; }
    }
}
