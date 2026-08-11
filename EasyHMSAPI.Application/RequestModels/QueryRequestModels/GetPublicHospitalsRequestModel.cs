using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Platform-wide list of publicly-listed hospitals -- e.g. the WhatsApp bot's hospital-name
    // matching (resolver.match_hospital_by_query), which has nothing to fuzzy-match against
    // today (only GET public/hospitals/by-code/{code} exists, an exact single-code lookup).
    [ExcludeFromCodeCoverage]
    public class GetPublicHospitalsRequestModel : IRequest<GetPublicHospitalsResponseModel>
    {
    }
}
