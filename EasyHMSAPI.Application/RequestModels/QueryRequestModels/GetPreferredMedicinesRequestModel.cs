using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetPreferredMedicinesRequestModel : IRequest<List<GetPreferredMedicineResponseModel>>
    {
        public Guid DoctorId { get; set; }
    }
}
