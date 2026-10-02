using System.Text.RegularExpressions;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>Validation rules for Health Wiki articles, kept out of the handler so they are unit-testable.</summary>
    public static class HealthArticleRules
    {
        public const int MaxSlugLength = 200;
        public const int MaxTitleLength = 300;
        public const int MaxDescriptionLength = 1000;

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
    }
}
