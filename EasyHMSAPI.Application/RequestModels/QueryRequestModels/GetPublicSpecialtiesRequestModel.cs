using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Platform-wide list of specialty categories with at least one publicly bookable doctor —
    // feeds picklists (e.g. the WhatsApp booking bot, NexEagleWebsite) that need to offer only
    // categories a patient can actually book into, without paging through GetPublicDoctors and
    // grouping client-side.
    [ExcludeFromCodeCoverage]
    public class GetPublicSpecialtiesRequestModel : IRequest<GetPublicSpecialtiesResponseModel>
    {
    }
}
