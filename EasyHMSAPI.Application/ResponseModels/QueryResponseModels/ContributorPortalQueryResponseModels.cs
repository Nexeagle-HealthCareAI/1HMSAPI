using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetContributorMeResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public ContributorMeInfo? Profile { get; set; }

        public static GetContributorMeResponseModel Ok(ContributorMeInfo profile) => new() { Success = true, StatusCode = 200, Profile = profile };
        public static GetContributorMeResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    // The contributor's articles grouped as the pages show them.
    [ExcludeFromCodeCoverage]
    public class GetContributorArticlesResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public MyArticleListInfo? Articles { get; set; }

        public static GetContributorArticlesResponseModel Ok(MyArticleListInfo articles) => new() { Success = true, StatusCode = 200, Articles = articles };
        public static GetContributorArticlesResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    [ExcludeFromCodeCoverage]
    public class GetContributorArticleResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public MyArticleInfo? Article { get; set; }

        public static GetContributorArticleResponseModel Ok(MyArticleInfo article) => new() { Success = true, StatusCode = 200, Article = article };
        public static GetContributorArticleResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    [ExcludeFromCodeCoverage]
    public class ContributorArticleHistoryEntry
    {
        public DateTime At { get; set; }
        // The action code, so the page can pick an icon and colour.
        public string Kind { get; set; } = null!;
        public string Action { get; set; } = null!;
        // The person's name, or "NexEagle team".
        public string Actor { get; set; } = null!;
        public bool ByMe { get; set; }
        // A reason or comment written for the doctor, only on the actions that carry one.
        public string? Detail { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetContributorArticleHistoryResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public List<ContributorArticleHistoryEntry> Entries { get; set; } = new();

        public static GetContributorArticleHistoryResponseModel Ok(List<ContributorArticleHistoryEntry> entries) => new() { Success = true, StatusCode = 200, Entries = entries };
        public static GetContributorArticleHistoryResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    [ExcludeFromCodeCoverage]
    public class GetContributorTopicsResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public List<MyTopicInfo> Topics { get; set; } = new();

        public static GetContributorTopicsResponseModel Ok(List<MyTopicInfo> topics) => new() { Success = true, StatusCode = 200, Topics = topics };
        public static GetContributorTopicsResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    // Topic requests for the CMS inbox: all of them, or the one asked for.
    [ExcludeFromCodeCoverage]
    public class GetTopicRequestsAdminResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public List<TopicRequestAdminInfo> Topics { get; set; } = new();

        public static GetTopicRequestsAdminResponseModel Ok(List<TopicRequestAdminInfo> topics) => new() { Success = true, StatusCode = 200, Topics = topics };
        public static GetTopicRequestsAdminResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
