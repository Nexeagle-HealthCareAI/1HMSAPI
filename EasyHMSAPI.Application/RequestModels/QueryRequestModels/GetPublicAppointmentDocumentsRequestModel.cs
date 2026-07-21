using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Mobile is set by the controller from an already-validated patient JWT claim — never a
    // client-supplied query param (same convention as GetPublicAppointmentsByMobileRequestModel).
    // AppointmentId comes from the route; the handler still re-checks that this mobile actually
    // owns that appointment (via PatientRegistration.Mobile) before returning any documents.
    [ExcludeFromCodeCoverage]
    public class GetPublicAppointmentDocumentsRequestModel : IRequest<GetPublicAppointmentDocumentsResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
        public Guid AppointmentId { get; set; }
    }
}
