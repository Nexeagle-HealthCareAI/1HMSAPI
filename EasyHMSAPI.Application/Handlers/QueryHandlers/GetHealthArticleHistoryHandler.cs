using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Who did what to an article, newest first.</summary>
    public class GetHealthArticleHistoryHandler : IRequestHandler<GetHealthArticleHistoryRequestModel, GetHealthArticleHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHealthArticleHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHealthArticleHistoryResponseModel> Handle(GetHealthArticleHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var slug = request.Slug?.Trim().ToLowerInvariant();
            var articleId = await _context.HealthArticles.AsNoTracking()
                .Where(a => a.Slug == slug).Select(a => (Guid?)a.ArticleId).FirstOrDefaultAsync(cancellationToken);
            if (articleId == null) return new GetHealthArticleHistoryResponseModel { Found = false };

            var rows = await _context.HealthWikiAudits.AsNoTracking()
                .Where(h => h.EntityType == HealthWikiAudit.EntityArticle && h.EntityId == articleId.Value)
                .OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.AuditId)
                .ToListAsync(cancellationToken);

            return new GetHealthArticleHistoryResponseModel
            {
                Found = true,
                Entries = rows.Select(h => new HealthArticleHistoryEntry
                {
                    At = h.CreatedAt,
                    Actor = h.ActorName ?? h.ActorType,
                    Action = HealthWikiAuditLog.Label(h.Action),
                    Detail = h.Detail,
                }).ToList(),
            };
        }
    }
}
