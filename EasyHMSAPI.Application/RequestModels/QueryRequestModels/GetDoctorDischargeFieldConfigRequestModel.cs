using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetDoctorDischargeFieldConfigRequestModel : IRequest<GetDoctorDischargeFieldConfigResponseModel>
    {
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; }
    }
}
