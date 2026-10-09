using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    internal static class ContributorArticleLoader
    {
        /// <summary>The articles a person wrote or must review, with their open edits and the names to show.</summary>
        public static async Task<List<MyArticleInfo>> Load(AppDbContext context, Guid me, string? slug, CancellationToken ct)
        {
            var query = context.HealthArticles.AsNoTracking().Where(a => a.AuthorContributorId == me || a.ReviewerContributorId == me);
            if (slug != null) query = query.Where(a => a.Slug == slug);
            var articles = await query.OrderByDescending(a => a.UpdatedAt).Take(500).ToListAsync(ct);
            if (articles.Count == 0) return new List<MyArticleInfo>();

            var ids = articles.Select(a => a.ArticleId).ToList();
            var open = (await context.HealthArticleRevisions.AsNoTracking()
                .Where(r => ids.Contains(r.ArticleId) && (r.Status == HealthArticleRevision.StatusDraft || r.Status == HealthArticleRevision.StatusInReview))
                .ToListAsync(ct)).ToDictionary(r => r.ArticleId);
            var peopleIds = articles.SelectMany(a => new[] { a.AuthorContributorId, a.ReviewerContributorId }).Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
            var names = await context.HealthWikiContributors.AsNoTracking().Where(c => peopleIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c.FullName, ct);

            var now = DateTime.UtcNow;
            string? Name(Guid? id) => id.HasValue && names.TryGetValue(id.Value, out var n) ? n : null;

            var result = new List<MyArticleInfo>();
            foreach (var a in articles)
            {
                var revision = open.GetValueOrDefault(a.ArticleId);
                var isAuthor = a.AuthorContributorId == me;
                // A reviewer sees an article only once it is with them: in review, live, or with an edit in review.
                if (!isAuthor)
                {
                    var visible = a.Status != HealthArticle.StatusDraft || revision?.Status == HealthArticleRevision.StatusInReview;
                    if (!visible) continue;
                }
                result.Add(ContributorPortalMapper.ToMyArticle(a, revision, me, Name(a.AuthorContributorId), Name(a.ReviewerContributorId), now));
            }
            return result;
        }
    }

    public class GetContributorArticlesHandler : IRequestHandler<GetContributorArticlesRequestModel, ApiResult<MyArticleListInfo>>
    {
        private readonly AppDbContext _context;

        public GetContributorArticlesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<MyArticleListInfo>> Handle(GetContributorArticlesRequestModel request, CancellationToken ct)
        {
            var all = await ContributorArticleLoader.Load(_context, request.ContributorId, null, ct);
            bool Reviewer(MyArticleInfo a) => a.MyRole == ContributorPortalMapper.RoleReviewer;

            return ApiResult<MyArticleListInfo>.Ok(new MyArticleListInfo
            {
                // Waiting for MY decision: in review and not yet approved by me, or a live article with an edit in review.
                Pending = all.Where(a => Reviewer(a) && !a.AwaitingVerification
                                         && (a.Status == HealthArticle.StatusInReview || a.RevisionStatus == HealthArticleRevision.StatusInReview))
                             .OrderByDescending(a => a.WaitingDays ?? 0).ToList(),
                Published = all.Where(a => a.Status == HealthArticle.StatusPublished).OrderByDescending(a => a.UpdatedAt).ToList(),
                Drafts = all.Where(a => !Reviewer(a) && a.Status != HealthArticle.StatusPublished).OrderByDescending(a => a.UpdatedAt).ToList(),
            });
        }
    }

    public class GetContributorArticleHandler : IRequestHandler<GetContributorArticleRequestModel, ApiResult<MyArticleInfo>>
    {
        private readonly AppDbContext _context;

        public GetContributorArticleHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<MyArticleInfo>> Handle(GetContributorArticleRequestModel request, CancellationToken ct)
        {
            var slug = request.Slug?.Trim().ToLowerInvariant();
            var found = (await ContributorArticleLoader.Load(_context, request.ContributorId, slug, ct)).FirstOrDefault();
            // Someone else's article reads the same as one that does not exist.
            return found == null ? ApiResult<MyArticleInfo>.Fail(404, "Article not found.") : ApiResult<MyArticleInfo>.Ok(found);
        }
    }
}
