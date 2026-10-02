using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Create (IsCreate) or partially update (null field = unchanged) a Health Wiki article.
    // Written by the CMS / EasyHMS via /internal/health-articles, never by Doctor Dekho.
    [ExcludeFromCodeCoverage]
    public class SaveHealthArticleRequestModel : IRequest<SaveHealthArticleResponseModel>
    {
        public bool IsCreate { get; set; }
        public string? Slug { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string? RelatedConditionSlug { get; set; }
        public Guid? AuthorDoctorId { get; set; }
        public Guid? ReviewerDoctorId { get; set; }
        // DRAFT | IN_REVIEW | PUBLISHED. Defaults to DRAFT on create.
        public string? Status { get; set; }
    }
}
