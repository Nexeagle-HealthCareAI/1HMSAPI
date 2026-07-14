using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Deliberately keyed only by AccessToken (no HospitalId/AdmissionId) -- this backs a fully
    // anonymous, unauthenticated endpoint (QR/WhatsApp "view discharge summary" link).
    [ExcludeFromCodeCoverage]
    public class GetPublicDischargeSummaryPdfRequestModel : IRequest<GetPublicDischargeSummaryPdfResponseModel>
    {
        public string AccessToken { get; set; } = null!;
    }
}
