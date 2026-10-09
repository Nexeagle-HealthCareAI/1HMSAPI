using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Makes an approved MEDICAL article live. The two things that must both be true are kept in one place:
    /// the reviewer approved it (ApprovedAt) and the reviewer's registration is VERIFIED.
    /// </summary>
    public static class HealthArticlePublisher
    {
        /// <summary>Publishes an article that is IN_REVIEW and approved. Returns false (and changes nothing) when it cannot go live yet.</summary>
        public static bool TryPublish(AppDbContext context, HealthArticle article, HealthWikiContributor reviewer, string actorName, DateTime now)
        {
            if (article.Type != HealthArticle.TypeMedical || article.Status != HealthArticle.StatusInReview
                || article.ApprovedAt == null || article.ReviewerContributorId != reviewer.ContributorId
                || reviewer.Status != HealthWikiContributor.StatusVerified)
                return false;

            article.Status = HealthArticle.StatusPublished;
            article.PublishedAt ??= now;
            article.ReviewerComment = null;
            article.UpdatedAt = now;
            HealthWikiAuditLog.Add(context, HealthWikiAudit.EntityArticle, article.ArticleId, "PUBLISHED", HealthWikiAudit.ActorSystem, actorName,
                $"Live after {reviewer.FullName}'s approval");
            return true;
        }

        /// <summary>
        /// A doctor whose registration the team has just verified: every article they approved while waiting goes live now.
        /// Returns how many were published.
        /// </summary>
        public static async Task<int> PublishWaitingFor(AppDbContext context, HealthWikiContributor reviewer, string actorName, DateTime now, CancellationToken ct)
        {
            var waiting = await context.HealthArticles
                .Where(a => a.ReviewerContributorId == reviewer.ContributorId && a.Status == HealthArticle.StatusInReview
                            && a.Type == HealthArticle.TypeMedical && a.ApprovedAt != null)
                .ToListAsync(ct);
            return waiting.Count(a => TryPublish(context, a, reviewer, actorName, now));
        }

        /// <summary>The edit is approved: its text replaces the live text.</summary>
        public static void ApplyRevision(AppDbContext context, HealthArticle article, HealthArticleRevision revision, string actorType, string actorName, DateTime now)
        {
            ArticleContent.From(revision).CopyTo(article);
            revision.Status = HealthArticleRevision.StatusApplied;
            revision.ResolvedAt = now;
            revision.UpdatedAt = now;
            article.UpdatedAt = now;
            HealthWikiAuditLog.Add(context, HealthWikiAudit.EntityArticle, article.ArticleId, "REVISION_APPLIED", actorType, actorName);
        }
    }
}
