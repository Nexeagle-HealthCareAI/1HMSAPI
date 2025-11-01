using System;
using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class DoctorBookedSlotsRequestModel : IRequest<DoctorBookedSlotsResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DateTime Date { get; set; }
    }
}
