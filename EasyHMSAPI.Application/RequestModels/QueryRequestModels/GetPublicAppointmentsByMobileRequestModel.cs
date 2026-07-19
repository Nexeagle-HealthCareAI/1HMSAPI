using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Mobile is set by the controller from an already-validated patient JWT claim — never a
    // client-supplied query param, or this would be a phone-number-enumeration lookup of anyone's
    // appointment history.
    [ExcludeFromCodeCoverage]
    public class GetPublicAppointmentsByMobileRequestModel : IRequest<GetPublicAppointmentsByMobileResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
    }
}
