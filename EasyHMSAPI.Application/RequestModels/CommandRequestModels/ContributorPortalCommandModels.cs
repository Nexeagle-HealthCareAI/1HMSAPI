using EasyHMSAPI.Application.ResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Every contributor request carries the signed-in contributor, set from the session by the controller, never from the body.
    [ExcludeFromCodeCoverage]
    public abstract class ContributorRequest
    {
        public Guid ContributorId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RegisterContributorRequestModel : ContributorRequest, IRequest<ApiResult<ContributorMeInfo>>
    {
        public string? FullName { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Bio { get; set; }
        // doctors
        public string? Speciality { get; set; }
        public string? Qualification { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? RegistrationCouncil { get; set; }
        public string? RegistrationYear { get; set; }
        // health workers and writers
        public string? RoleTitle { get; set; }
        public string? Organisation { get; set; }
        public string? FieldOfWork { get; set; }
        public bool Consent { get; set; }
    }

    // Create (Slug null) or edit (Slug set) the contributor's own article.
    [ExcludeFromCodeCoverage]
    public class SaveContributorArticleRequestModel : ContributorRequest, IRequest<ApiResult<MyArticleInfo>>
    {
        public string? Slug { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageAlt { get; set; }
        public string? RelatedConditionSlug { get; set; }
        public string? Disclosure { get; set; }
        public string? References { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SubmitContributorArticleRequestModel : ContributorRequest, IRequest<ApiResult<MyArticleInfo>>
    {
        public string Slug { get; set; } = null!;
    }

    // The assigned doctor's decision. APPROVE needs AccuracyConfirmed; REQUEST_CHANGES needs a comment of 10 or more characters.
    [ExcludeFromCodeCoverage]
    public class ReviewContributorArticleRequestModel : ContributorRequest, IRequest<ApiResult<MyArticleInfo>>
    {
        public const string Approve = "APPROVE";
        public const string RequestChanges = "REQUEST_CHANGES";

        public string Slug { get; set; } = null!;
        public string? Decision { get; set; }
        public string? Comment { get; set; }
        public bool AccuracyConfirmed { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CreateContributorTopicRequestModel : ContributorRequest, IRequest<ApiResult<MyTopicInfo>>
    {
        public string? Title { get; set; }
        public string? Type { get; set; }
        public string? Outline { get; set; }
        public string? WhyItMatters { get; set; }
        public string? ConditionSlug { get; set; }
        public string? References { get; set; }
    }

    // Answer a "needs detail" request: only the fields sent are changed, and the request goes back to SUBMITTED.
    [ExcludeFromCodeCoverage]
    public class AddContributorTopicDetailRequestModel : ContributorRequest, IRequest<ApiResult<MyTopicInfo>>
    {
        public Guid TopicId { get; set; }
        public string? Outline { get; set; }
        public string? WhyItMatters { get; set; }
        public string? ConditionSlug { get; set; }
        public string? References { get; set; }
        public HashSet<string> Provided { get; set; } = new();
    }

    // CMS: ACCEPT (creates a draft for the contributor), DECLINE (reason) or ASK_DETAIL (message).
    [ExcludeFromCodeCoverage]
    public class DecideTopicRequestModel : IRequest<ApiResult<TopicRequestAdminInfo>>
    {
        public const string Accept = "ACCEPT";
        public const string Decline = "DECLINE";
        public const string AskDetail = "ASK_DETAIL";

        public Guid TopicId { get; set; }
        public string Action { get; set; } = null!;
        public string? Note { get; set; }
        public string? ActorName { get; set; }
    }
}
