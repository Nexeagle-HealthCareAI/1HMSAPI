using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // A hospital doctor's decision on an article assigned to them. UserId comes from the verified JWT, never from the client.
    [ExcludeFromCodeCoverage]
    public class ReviewHealthWikiArticleRequestModel : IRequest<ReviewHealthWikiArticleResponseModel>
    {
        public Guid UserId { get; set; }
        public string Slug { get; set; } = null!;
        // APPROVE | REQUEST_CHANGES
        public string? Decision { get; set; }
        public string? Comment { get; set; }
        public bool AccuracyConfirmed { get; set; }
    }

    // A reader taps "Helpful". VisitorKey is an anonymous id the Doctor Dekho site keeps for the reader.
    [ExcludeFromCodeCoverage]
    public class LikeHealthArticleRequestModel : IRequest<HealthArticleReactionResponseModel>
    {
        public string? Slug { get; set; }
        public string? VisitorKey { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordHealthArticleViewRequestModel : IRequest<HealthArticleReactionResponseModel>
    {
        public string? Slug { get; set; }
        public string? VisitorKey { get; set; }
    }
}
