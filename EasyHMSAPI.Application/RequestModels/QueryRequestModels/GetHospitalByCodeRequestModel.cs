using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Resolves a scanned OPD QR code to a hospital. Public/anonymous -- the bot gateway calls this
    // with no patient session, purely from the printed HospitalCode.
    [ExcludeFromCodeCoverage]
    public class GetHospitalByCodeRequestModel : IRequest<GetHospitalByCodeResponseModel>
    {
        public string HospitalCode { get; set; } = string.Empty;
    }
}
