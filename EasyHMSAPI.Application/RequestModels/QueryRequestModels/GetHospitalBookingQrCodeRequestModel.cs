using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Renders the appointment-booking QR poster (NexEagle logo composited at center) for a
    // hospital that already has a HospitalCode -- deliberately a pure read, no side effects, so
    // it does NOT auto-generate a code (see GenerateHospitalCodeHandler for that step). Same
    // HospitalCode as the OPD check-in QR (GetHospitalQrCodeRequestModel), just encoded with the
    // bot's "/h/" (hospital booking) redirect instead of "/c/" (check-in).
    [ExcludeFromCodeCoverage]
    public class GetHospitalBookingQrCodeRequestModel : IRequest<GetHospitalBookingQrCodeResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
