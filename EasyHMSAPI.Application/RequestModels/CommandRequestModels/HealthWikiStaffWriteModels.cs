using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // A hospital doctor chose "Write an article" in EasyHMS. The answer is a Doctor Dekho address that signs them in once. UserId comes from the verified JWT.
    [ExcludeFromCodeCoverage]
    public class CreateHealthWikiWriteHandoffRequestModel : IRequest<CreateHealthWikiWriteHandoffResponseModel>
    {
        public Guid UserId { get; set; }
        // Where to land on Doctor Dekho: "write" (a new article, the default) or "drafts" (My drafts).
        public string? To { get; set; }
    }

    // Doctor Dekho swaps the one-time code for a session.
    [ExcludeFromCodeCoverage]
    public class ContributorSsoExchangeRequestModel : IRequest<ContributorOtpVerifyResponseModel>
    {
        public string? Code { get; set; }
    }

    // A hospital doctor's own topic requests in EasyHMS. The doctor is the signed-in user.
    [ExcludeFromCodeCoverage]
    public class GetHealthWikiStaffTopicsRequestModel : IRequest<GetContributorTopicsResponseModel>
    {
        public Guid UserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CreateHealthWikiStaffTopicRequestModel : IRequest<ContributorTopicResponseModel>
    {
        public Guid UserId { get; set; }
        public string? Title { get; set; }
        public string? Type { get; set; }
        public string? Outline { get; set; }
        public string? WhyItMatters { get; set; }
        public string? ConditionSlug { get; set; }
        public string? References { get; set; }
        public bool RequestWriter { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AddHealthWikiStaffTopicDetailRequestModel : IRequest<ContributorTopicResponseModel>
    {
        public Guid UserId { get; set; }
        public Guid TopicId { get; set; }
        public string? Outline { get; set; }
        public string? WhyItMatters { get; set; }
        public string? ConditionSlug { get; set; }
        public string? References { get; set; }
        // Names of the fields that were in the request, so a field left out stays as it is.
        public HashSet<string> Provided { get; set; } = new();
    }
}
