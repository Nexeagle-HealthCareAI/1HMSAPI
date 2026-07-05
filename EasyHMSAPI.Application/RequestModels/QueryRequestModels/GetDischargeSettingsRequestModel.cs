using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetDischargeSettingsRequestModel : IRequest<GetDischargeSettingsResponseModel>
    {
        public Guid DoctorId { get; set; }
        public Guid HospitalId { get; set; }
    }
}
