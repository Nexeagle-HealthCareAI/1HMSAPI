using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    public static class ContributorPortalMapper
    {
        public const string RoleAuthor = "AUTHOR";
        public const string RoleReviewer = "REVIEWER";

        public static ContributorMeInfo ToMe(HealthWikiContributor c) => new()
        {
            Id = c.ContributorId,
            Role = ContributorPortalRules.EnrolRole(c.Type),
            FullName = c.FullName,
            MobileMasked = c.Mobile == null ? "••••••••••" : ContributorSecurity.MaskMobile(c.Mobile),
            Status = c.Status,
            Speciality = c.Speciality,
            Qualification = c.Qualification,
            RoleTitle = c.RoleTitle,
            Organisation = c.Organisation,
            RegistrationNumber = c.RegistrationNumber,
            RegistrationCouncil = c.RegistrationCouncil,
            RejectReason = c.RejectReason,
        };

        /// <summary>
        /// The article as one of its people sees it. The text is the working copy (the open edit if there is one). The reviewer's
        /// view of an article they approved before being verified reads PUBLISHED with AwaitingVerification, as the pages expect.
        /// </summary>
        public static MyArticleInfo ToMyArticle(HealthArticle a, HealthArticleRevision? revision, Guid me, string? authorName, string? reviewerName, DateTime now)
        {
            var text = revision != null ? ArticleContent.From(revision) : ArticleContent.From(a);
            var role = a.AuthorContributorId == me ? RoleAuthor : RoleReviewer;
            var awaiting = role == RoleReviewer && a.Status == HealthArticle.StatusInReview && a.ApprovedAt != null;
            var inReview = a.Status == HealthArticle.StatusInReview || revision?.Status == HealthArticleRevision.StatusInReview;
            var live = a.Status == HealthArticle.StatusPublished;

            return new MyArticleInfo
            {
                Slug = a.Slug,
                Type = a.Type,
                Title = text.Title,
                Description = text.Description ?? string.Empty,
                Content = text.Content,
                CoverImageUrl = text.CoverImageUrl,
                CoverImageAlt = text.CoverImageAlt,
                RelatedConditionSlug = text.RelatedConditionSlug,
                Disclosure = text.Disclosure,
                References = text.References,
                Status = awaiting ? HealthArticle.StatusPublished : a.Status,
                MyRole = role,
                AuthorName = authorName ?? string.Empty,
                ReviewerName = reviewerName,
                ReturnedComment = revision?.ReviewerComment ?? (a.Status == HealthArticle.StatusDraft ? a.ReviewerComment : null),
                WaitingDays = inReview && a.SubmittedAt.HasValue && !awaiting ? Math.Max(0, (int)(now - a.SubmittedAt.Value).TotalDays) : null,
                AwaitingVerification = awaiting,
                Views = live ? a.ViewCount : null,
                Likes = live ? a.LikeCount : null,
                PublishedAt = a.PublishedAt,
                UpdatedAt = revision?.UpdatedAt ?? a.UpdatedAt,
                HasPendingRevision = revision != null,
                RevisionStatus = revision?.Status,
            };
        }

        public static MyTopicInfo ToMyTopic(HealthWikiTopicRequest t, string? articleSlug) => new()
        {
            Id = t.TopicId, Title = t.Title, Type = t.Type, Outline = t.Outline, WhyItMatters = t.WhyItMatters ?? string.Empty,
            ConditionSlug = t.ConditionSlug, References = t.References, Status = t.Status, DecisionNote = t.DecisionNote,
            ArticleSlug = articleSlug, CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt,
        };

        public static TopicRequestAdminInfo ToAdminTopic(HealthWikiTopicRequest t, string? articleSlug) => new()
        {
            TopicId = t.TopicId, ContributorId = t.ContributorId, Title = t.Title, Type = t.Type, Outline = t.Outline,
            WhyItMatters = t.WhyItMatters ?? string.Empty, ConditionSlug = t.ConditionSlug, References = t.References, Status = t.Status,
            DecisionReason = t.DecisionNote, ArticleSlug = articleSlug, CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt,
        };
    }
}
