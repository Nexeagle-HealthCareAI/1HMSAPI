using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class RegisterContributorResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public ContributorMeInfo? Profile { get; set; }

        public static RegisterContributorResponseModel Ok(ContributorMeInfo profile) => new() { Success = true, StatusCode = 200, Profile = profile };
        public static RegisterContributorResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    // Create, edit, submit and review of a contributor's article all answer with the article as the person sees it.
    [ExcludeFromCodeCoverage]
    public class ContributorArticleResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public MyArticleInfo? Article { get; set; }

        public static ContributorArticleResponseModel Ok(MyArticleInfo article, int status = 200) => new() { Success = true, StatusCode = status, Article = article };
        public static ContributorArticleResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    // Creating a topic and adding detail to one both answer with the contributor's own view of it.
    [ExcludeFromCodeCoverage]
    public class ContributorTopicResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public MyTopicInfo? Topic { get; set; }

        public static ContributorTopicResponseModel Ok(MyTopicInfo topic, int status = 200) => new() { Success = true, StatusCode = status, Topic = topic };
        public static ContributorTopicResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    // The CMS decision on a topic request answers with the request as the CMS sees it.
    [ExcludeFromCodeCoverage]
    public class DecideTopicResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public TopicRequestAdminInfo? Topic { get; set; }

        public static DecideTopicResponseModel Ok(TopicRequestAdminInfo topic) => new() { Success = true, StatusCode = 200, Topic = topic };
        public static DecideTopicResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
