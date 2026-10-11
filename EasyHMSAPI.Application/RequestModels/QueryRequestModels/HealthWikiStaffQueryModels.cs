using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // The signed-in hospital doctor's Health Wiki inbox. UserId comes from the verified JWT, never from the client.
    [ExcludeFromCodeCoverage]
    public class GetHealthWikiInboxRequestModel : IRequest<GetHealthWikiInboxResponseModel>
    {
        public Guid UserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthWikiStaffArticleRequestModel : IRequest<GetHealthWikiStaffArticleResponseModel>
    {
        public Guid UserId { get; set; }
        public string Slug { get; set; } = null!;
    }
}
