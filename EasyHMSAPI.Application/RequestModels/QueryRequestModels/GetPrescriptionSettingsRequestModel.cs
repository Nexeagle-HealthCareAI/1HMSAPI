using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPrescriptionSettingsRequestModel : IRequest<GetPrescriptionSettingsResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
