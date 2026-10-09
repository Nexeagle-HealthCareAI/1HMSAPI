using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Create (IsCreate) or partially update a Health Wiki article. Written by the CMS API via
    // /internal/health-articles, never by Doctor Dekho.
    //
    // Partial update: a field that is absent from the JSON is left unchanged, while a field sent as null is
    // cleared. The controller lists the JSON keys it saw in Provided. When Provided is null (a caller that
    // builds this model in code) every non-null property counts as provided.
    [ExcludeFromCodeCoverage]
    public class SaveHealthArticleRequestModel : IRequest<SaveHealthArticleResponseModel>
    {
        public bool IsCreate { get; set; }
        public string? Slug { get; set; }
        // MEDICAL | SECTOR_UPDATE. Defaults to MEDICAL on create.
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string? RelatedConditionSlug { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageAlt { get; set; }
        public string? Disclosure { get; set; }
        public string? References { get; set; }
        public Guid? AuthorContributorId { get; set; }
        public Guid? ReviewerContributorId { get; set; }
        // DRAFT | IN_REVIEW | PUBLISHED. Defaults to DRAFT on create.
        public string? Status { get; set; }
        // Who made the change, for the history. The CMS API passes the signed-in CMS user.
        public string? ActorName { get; set; }
        // A contributor may save a draft before the text is written. Ignored once the article is sent for review or published.
        public bool AllowEmptyContent { get; set; }
        // CMS_USER (default) or CONTRIBUTOR, for the history.
        public string? ActorType { get; set; }

        // camelCase JSON keys the caller sent.
        public HashSet<string>? Provided { get; set; }

        public bool Has(string key, object? value) => Provided != null ? Provided.Contains(key) : value != null;
    }
}
