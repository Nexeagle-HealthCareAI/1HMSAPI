using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    public static class HealthArticleAdminMapper
    {
        /// <summary>The CMS view of an article: the open edit's text when there is one, otherwise the live text.</summary>
        public static HealthArticleAdminInfo ToInfo(HealthArticle a, HealthArticleRevision? openRevision, (Guid Id, string Name)? suggester = null)
        {
            var text = openRevision != null ? ArticleContent.From(openRevision) : ArticleContent.From(a);
            return new HealthArticleAdminInfo
            {
                Slug = a.Slug,
                Type = a.Type,
                Title = text.Title,
                Description = text.Description,
                Content = text.Content,
                RelatedConditionSlug = text.RelatedConditionSlug,
                CoverImageUrl = text.CoverImageUrl,
                CoverImageAlt = text.CoverImageAlt,
                Disclosure = text.Disclosure,
                References = text.References,
                AuthorContributorId = a.AuthorContributorId,
                ReviewerContributorId = a.ReviewerContributorId,
                Status = a.Status,
                ReviewerComment = openRevision?.ReviewerComment ?? a.ReviewerComment,
                PublishedAt = a.PublishedAt,
                UpdatedAt = openRevision?.UpdatedAt ?? a.UpdatedAt,
                HasPendingRevision = openRevision != null,
                HoldForReview = a.HoldForReview,
                SuggestedByContributorId = suggester?.Id,
                SuggestedByName = suggester?.Name,
                RevisionStatus = openRevision?.Status,
                ViewCount = a.ViewCount,
                LikeCount = a.LikeCount,
            };
        }
    }
}
