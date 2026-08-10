using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Idempotent -- if the hospital already has a HospitalCode, returns the existing one rather
    // than issuing a new one (a printed QR code must never silently stop working).
    [ExcludeFromCodeCoverage]
    public class GenerateHospitalCodeRequestModel : IRequest<GenerateHospitalCodeResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
