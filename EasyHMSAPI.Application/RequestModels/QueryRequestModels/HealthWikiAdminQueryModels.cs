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

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetContributorMeRequestModel : IRequest<EasyHMSAPI.Application.ResponseModels.ApiResult<ContributorMeInfo>>
    {
        public Guid ContributorId { get; set; }
    }

    // The contributor's articles, grouped; with Slug set, one article (404 unless they wrote it or must review it).
    [ExcludeFromCodeCoverage]
    public class GetContributorArticlesRequestModel : IRequest<EasyHMSAPI.Application.ResponseModels.ApiResult<MyArticleListInfo>>
    {
        public Guid ContributorId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetContributorArticleRequestModel : IRequest<EasyHMSAPI.Application.ResponseModels.ApiResult<MyArticleInfo>>
    {
        public Guid ContributorId { get; set; }
        public string Slug { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class GetContributorTopicsRequestModel : IRequest<EasyHMSAPI.Application.ResponseModels.ApiResult<List<MyTopicInfo>>>
    {
        public Guid ContributorId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetTopicRequestsAdminRequestModel : IRequest<EasyHMSAPI.Application.ResponseModels.ApiResult<List<TopicRequestAdminInfo>>>
    {
        // One topic when set.
        public Guid? TopicId { get; set; }
        public string? Status { get; set; }
    }
}
