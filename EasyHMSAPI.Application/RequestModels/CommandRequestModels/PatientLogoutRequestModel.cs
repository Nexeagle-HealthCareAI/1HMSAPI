using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Mobile is set by the controller from the already-validated patient JWT claim — never
    // trust a client-supplied mobile here, or any caller could log another patient's sessions out.
    [ExcludeFromCodeCoverage]
    public class PatientLogoutRequestModel : IRequest<PatientLogoutResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
    }
}
