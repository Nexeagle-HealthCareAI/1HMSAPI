using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // The Doctor Dekho app's start-up read. Mobile is set by the controller from the verified patient token, never from the client.
    [ExcludeFromCodeCoverage]
    public class GetPatientAppProfileRequestModel : IRequest<PatientAppProfileResponseModel>
    {
        public string Mobile { get; set; } = string.Empty;
    }
}
