using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // All articles in any status, for the CMS. Slug set = one article.
    [ExcludeFromCodeCoverage]
    public class GetHealthArticlesAdminRequestModel : IRequest<GetHealthArticlesAdminResponseModel>
    {
        public string? Slug { get; set; }
        public string? Status { get; set; }
        public string? Type { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthArticleHistoryRequestModel : IRequest<GetHealthArticleHistoryResponseModel>
    {
        public string Slug { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthWikiConditionsRequestModel : IRequest<GetHealthWikiConditionsResponseModel> { }

    [ExcludeFromCodeCoverage]
    public class GetHealthWikiSummaryRequestModel : IRequest<GetHealthWikiSummaryResponseModel> { }
}

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetContributorInviteRequestModel : IRequest<ContributorInviteInfo>
    {
        public string? Token { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetContributorsAdminRequestModel : IRequest<GetContributorsAdminResponseModel>
    {
        public string? Status { get; set; }
        public string? Type { get; set; }
    }
}
