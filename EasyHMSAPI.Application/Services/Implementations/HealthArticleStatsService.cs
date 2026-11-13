using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class HealthArticleStatsService : IHealthArticleStatsService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        public HealthArticleStatsService(AppDbContext context, IMemoryCache cache, IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _configuration = configuration;
        }

        public async Task<HealthArticleReactionResult> LikeAsync(string? slug, string? visitorKey, CancellationToken ct)
        {
            var (article, hash, failure) = await Resolve(slug, visitorKey, ct);
            if (failure != null) return failure;

            if (await _context.HealthArticleLikes.AsNoTracking().AnyAsync(l => l.ArticleId == article!.ArticleId && l.VisitorKeyHash == hash, ct))
                return await Totals(article!.ArticleId, changed: false, ct);

            _context.HealthArticleLikes.Add(new HealthArticleLike { ArticleId = article!.ArticleId, VisitorKeyHash = hash!, CreatedAt = DateTime.UtcNow });
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Two taps at the same moment: the primary key (article, visitor) refused the second. This reader already said Helpful.
                _context.ChangeTracker.Clear();
                return await Totals(article.ArticleId, changed: false, ct);
            }

            await _context.HealthArticles.Where(a => a.ArticleId == article.ArticleId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.LikeCount, a => a.LikeCount + 1), ct);
            await BumpDaily(article.ArticleId, views: 0, likes: 1, ct);
            return await Totals(article.ArticleId, changed: true, ct);
        }

        public async Task<HealthArticleReactionResult> ViewAsync(string? slug, string? visitorKey, CancellationToken ct)
        {
            var (article, hash, failure) = await Resolve(slug, visitorKey, ct);
            if (failure != null) return failure;

            // One view per reader per article per day. Kept in memory: it only has to stop a reload from counting twice, and a restart
            // costing a few extra views is harmless.
            var day = DateTime.UtcNow.Date;
            var key = $"hwv:{article!.ArticleId:N}:{hash}:{day:yyyyMMdd}";
            if (_cache.TryGetValue(key, out _)) return await Totals(article.ArticleId, changed: false, ct);
            _cache.Set(key, true, TimeSpan.FromHours(25));

            await _context.HealthArticles.Where(a => a.ArticleId == article.ArticleId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.ViewCount, a => a.ViewCount + 1), ct);
            await BumpDaily(article.ArticleId, views: 1, likes: 0, ct);
            return await Totals(article.ArticleId, changed: true, ct);
        }

        // Only a published article takes reactions; the visitor key becomes a keyed hash and is never stored raw.
        private async Task<(HealthArticle? Article, string? Hash, HealthArticleReactionResult? Failure)> Resolve(string? slugInput, string? visitorKey, CancellationToken ct)
        {
            var key = visitorKey?.Trim();
            if (string.IsNullOrEmpty(key) || key.Length > HealthArticleRules.MaxVisitorKeyLength)
                return (null, null, HealthArticleReactionResult.Fail(400, "visitorKey is required (max 128 characters)."));

            var secret = _configuration["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret) || secret.StartsWith("<"))
                return (null, null, HealthArticleReactionResult.Fail(503, "This is not available right now."));

            var slug = slugInput?.Trim().ToLowerInvariant();
            var article = await _context.HealthArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.Status == HealthArticle.StatusPublished, ct);
            if (article == null) return (null, null, HealthArticleReactionResult.Fail(404, "Article not found."));

            return (article, HealthArticleRules.HashVisitorKey(key, article.ArticleId, secret), null);
        }

        // Add to today's row, creating it on the first reaction of the day. Two readers racing to create it: the loser updates instead.
        private async Task BumpDaily(Guid articleId, int views, int likes, CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date;
            async Task<int> Update() => await _context.HealthArticleStatDailies.Where(d => d.ArticleId == articleId && d.StatDate == today)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Views, d => d.Views + views).SetProperty(d => d.Likes, d => d.Likes + likes), ct);

            if (await Update() > 0) return;
            try
            {
                _context.HealthArticleStatDailies.Add(new HealthArticleStatDaily { ArticleId = articleId, StatDate = today, Views = views, Likes = likes });
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                await Update();
            }
        }

        private async Task<HealthArticleReactionResult> Totals(Guid articleId, bool changed, CancellationToken ct)
        {
            var totals = await _context.HealthArticles.AsNoTracking().Where(a => a.ArticleId == articleId)
                .Select(a => new { a.ViewCount, a.LikeCount }).FirstAsync(ct);
            return new HealthArticleReactionResult { Success = true, StatusCode = 200, Changed = changed, Views = totals.ViewCount, Likes = totals.LikeCount };
        }
    }
}
