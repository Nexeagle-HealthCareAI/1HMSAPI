using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Renders the OPD check-in QR poster (NexEagle logo composited at center) for a hospital
    // that already has a HospitalCode -- deliberately a pure read, no side effects, so it does
    // NOT auto-generate a code (see GenerateHospitalCodeHandler for that step).
    [ExcludeFromCodeCoverage]
    public class GetHospitalQrCodeRequestModel : IRequest<GetHospitalQrCodeResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
