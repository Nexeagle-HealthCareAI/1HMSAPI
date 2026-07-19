using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Mobile is set by the controller from an already-validated patient JWT claim — never a
    // client-supplied query param (same reasoning as GetPublicAppointmentsByMobileRequestModel).
    [ExcludeFromCodeCoverage]
    public class GetPublicPatientProfileRequestModel : IRequest<GetPublicPatientProfileResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
    }
}
