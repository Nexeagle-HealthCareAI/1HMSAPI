using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>All Health Wiki articles in any status, for the CMS (never exposed publicly).</summary>
    public class GetHealthArticlesAdminHandler : IRequestHandler<GetHealthArticlesAdminRequestModel, GetHealthArticlesAdminResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHealthArticlesAdminHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHealthArticlesAdminResponseModel> Handle(GetHealthArticlesAdminRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.HealthArticles.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var slug = request.Slug.Trim().ToLowerInvariant();
                query = query.Where(a => a.Slug == slug);
            }
            var status = HealthArticleRules.NormalizeStatus(request.Status);
            if (status != null) query = query.Where(a => a.Status == status);
            var type = HealthArticleRules.NormalizeType(request.Type);
            if (type != null) query = query.Where(a => a.Type == type);

            var articles = await query.OrderByDescending(a => a.UpdatedAt).ThenBy(a => a.Slug).ToListAsync(cancellationToken);
            var ids = articles.Select(a => a.ArticleId).ToList();
            var open = await _context.HealthArticleRevisions.AsNoTracking()
                .Where(r => ids.Contains(r.ArticleId) && (r.Status == HealthArticleRevision.StatusDraft || r.Status == HealthArticleRevision.StatusInReview))
                .ToListAsync(cancellationToken);
            var byArticle = open.ToDictionary(r => r.ArticleId);

            return new GetHealthArticlesAdminResponseModel
            {
                Articles = articles.Select(a => HealthArticleAdminMapper.ToInfo(a, byArticle.GetValueOrDefault(a.ArticleId))).ToList(),
            };
        }
    }
}
