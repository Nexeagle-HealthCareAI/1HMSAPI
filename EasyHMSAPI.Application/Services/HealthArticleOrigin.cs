using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Where an article came from when it was written for a topic someone suggested: that person's id and name. The CMS shows it to the
    /// editor and the reviewer is told "this topic was suggested by Dr. X". It is never part of the public page.
    /// </summary>
    public static class HealthArticleOrigin
    {
        public static async Task<Dictionary<Guid, (Guid Id, string Name)>> SuggestersOf(AppDbContext context, IReadOnlyCollection<Guid> articleIds, CancellationToken ct)
        {
            if (articleIds.Count == 0) return new();
            var rows = await (from t in context.HealthWikiTopicRequests.AsNoTracking()
                              join c in context.HealthWikiContributors.AsNoTracking() on t.ContributorId equals c.ContributorId
                              where t.ArticleId != null && articleIds.Contains(t.ArticleId.Value)
                              select new { ArticleId = t.ArticleId!.Value, c.ContributorId, c.FullName }).ToListAsync(ct);
            return rows.GroupBy(r => r.ArticleId).ToDictionary(g => g.Key, g => (g.First().ContributorId, g.First().FullName));
        }
    }
}
