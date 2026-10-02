using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Published Health Wiki articles for Doctor Dekho. Slug set = single-article lookup.
    [ExcludeFromCodeCoverage]
    public class GetPublicHealthArticlesRequestModel : IRequest<GetPublicHealthArticlesResponseModel>
    {
        public int PageSize { get; set; } = 100;
        public string? Slug { get; set; }
    }
}
