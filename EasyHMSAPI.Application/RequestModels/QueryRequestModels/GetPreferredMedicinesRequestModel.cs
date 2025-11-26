using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPreferredMedicinesRequestModel : IRequest<List<GetPreferredMedicineResponseModel>>
    {
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; } // Added hospitalId
    }
}
