using System.Text.RegularExpressions;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>Validation and trust rules for Health Wiki articles, kept out of the handlers so they are unit-testable.</summary>
    public static class HealthArticleRules
    {
        public const int MaxSlugLength = 200;
        public const int MaxTitleLength = 300;
        public const int MaxDescriptionLength = 1000;
        public const int MaxDisclosureLength = 1000;
        public const int MaxReferencesLength = 8000;
        public const int MaxImageUrlLength = 500;
        public const int MaxImageAltLength = 300;
        public const int MaxReasonLength = 1000;
        public const int MinChangeCommentLength = 10;

        // lowercase letters/digits separated by single hyphens, e.g. "diabetes-type-2"
        private static readonly Regex SlugPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

        public static bool IsValidSlug(string? slug) =>
            !string.IsNullOrEmpty(slug) && slug.Length <= MaxSlugLength && SlugPattern.IsMatch(slug);

        /// <summary>Canonical status (DRAFT / IN_REVIEW / PUBLISHED) or null when unrecognised.</summary>
        public static string? NormalizeStatus(string? status)
        {
            var s = status?.Trim().Replace(' ', '_').Replace('-', '_').ToUpperInvariant();
            return s switch
            {
                HealthArticle.StatusDraft => HealthArticle.StatusDraft,
                HealthArticle.StatusInReview => HealthArticle.StatusInReview,
                HealthArticle.StatusPublished => HealthArticle.StatusPublished,
                _ => null,
            };
        }

        /// <summary>Canonical article type (MEDICAL / SECTOR_UPDATE) or null when unrecognised.</summary>
        public static string? NormalizeType(string? type)
        {
            var s = type?.Trim().Replace(' ', '_').Replace('-', '_').ToUpperInvariant();
            return s switch
            {
                HealthArticle.TypeMedical => HealthArticle.TypeMedical,
                HealthArticle.TypeSectorUpdate => HealthArticle.TypeSectorUpdate,
                _ => null,
            };
        }

        public static bool IsDoctorType(string? contributorType) =>
            contributorType == HealthWikiContributor.TypeHospitalDoctor || contributorType == HealthWikiContributor.TypeIndependentDoctor;

        /// <summary>
        /// A medical article is written by a doctor or NexEagle staff. Health workers and writers write sector updates only.
        /// </summary>
        public static bool CanAuthor(string articleType, string contributorType) =>
            articleType == HealthArticle.TypeSectorUpdate
            || IsDoctorType(contributorType)
            || contributorType == HealthWikiContributor.TypeStaff;

        /// <summary>Only a doctor can review, and only a MEDICAL article has a reviewer.</summary>
        public static bool CanReview(string contributorType, string contributorStatus) =>
            IsDoctorType(contributorType) && contributorStatus != HealthWikiContributor.StatusRejected;

        /// <summary>The "Medically reviewed by" badge: only a verified doctor, only on a MEDICAL article.</summary>
        public static bool ShowsReviewerBadge(string articleType, string? reviewerType, string? reviewerStatus) =>
            articleType == HealthArticle.TypeMedical && IsDoctorType(reviewerType) && reviewerStatus == HealthWikiContributor.StatusVerified;

        /// <summary>https only, so a stored image URL can never be plain http or a script URL.</summary>
        public static bool IsHttpsUrl(string? url) =>
            !string.IsNullOrWhiteSpace(url) && url.Length <= MaxImageUrlLength
            && Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;

        /// <summary>First problem with the text fields of an article, or null when they are acceptable.</summary>
        public static string? ValidateContent(ArticleContent c, bool requireContent = true)
        {
            if (string.IsNullOrWhiteSpace(c.Title) || c.Title.Length > MaxTitleLength)
                return $"title is required (max {MaxTitleLength} chars).";
            if (c.Description != null && c.Description.Length > MaxDescriptionLength)
                return $"description is max {MaxDescriptionLength} chars.";
            if (requireContent && string.IsNullOrWhiteSpace(c.Content))
                return "content cannot be empty.";
            if (c.RelatedConditionSlug != null && !IsValidSlug(c.RelatedConditionSlug))
                return "relatedConditionSlug must be lowercase letters/digits separated by single hyphens.";
            if (c.Disclosure != null && c.Disclosure.Length > MaxDisclosureLength)
                return $"disclosure is max {MaxDisclosureLength} chars.";
            if (c.References != null && c.References.Length > MaxReferencesLength)
                return $"references is max {MaxReferencesLength} chars.";
            if (c.CoverImageUrl != null)
            {
                if (!IsHttpsUrl(c.CoverImageUrl)) return "coverImageUrl must be an https URL.";
                if (string.IsNullOrWhiteSpace(c.CoverImageAlt)) return "A cover image needs alt text (coverImageAlt).";
            }
            if (c.CoverImageAlt != null && c.CoverImageAlt.Length > MaxImageAltLength)
                return $"coverImageAlt is max {MaxImageAltLength} chars.";
            return null;
        }
    }

    /// <summary>
    /// The text fields of an article. The live article and its pending revision hold the same set,
    /// so edits are built here once and then copied to whichever of the two they belong to.
    /// </summary>
    public class ArticleContent
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? RelatedConditionSlug { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageAlt { get; set; }
        public string? Disclosure { get; set; }
        public string? References { get; set; }

        public static ArticleContent From(HealthArticle a) => new()
        {
            Title = a.Title, Description = a.Description, Content = a.Content, RelatedConditionSlug = a.RelatedConditionSlug,
            CoverImageUrl = a.CoverImageUrl, CoverImageAlt = a.CoverImageAlt, Disclosure = a.Disclosure, References = a.References,
        };

        public static ArticleContent From(HealthArticleRevision r) => new()
        {
            Title = r.Title, Description = r.Description, Content = r.Content, RelatedConditionSlug = r.RelatedConditionSlug,
            CoverImageUrl = r.CoverImageUrl, CoverImageAlt = r.CoverImageAlt, Disclosure = r.Disclosure, References = r.References,
        };

        public void CopyTo(HealthArticle a)
        {
            a.Title = Title; a.Description = Description; a.Content = Content; a.RelatedConditionSlug = RelatedConditionSlug;
            a.CoverImageUrl = CoverImageUrl; a.CoverImageAlt = CoverImageAlt; a.Disclosure = Disclosure; a.References = References;
        }

        public void CopyTo(HealthArticleRevision r)
        {
            r.Title = Title; r.Description = Description; r.Content = Content; r.RelatedConditionSlug = RelatedConditionSlug;
            r.CoverImageUrl = CoverImageUrl; r.CoverImageAlt = CoverImageAlt; r.Disclosure = Disclosure; r.References = References;
        }
    }
}
