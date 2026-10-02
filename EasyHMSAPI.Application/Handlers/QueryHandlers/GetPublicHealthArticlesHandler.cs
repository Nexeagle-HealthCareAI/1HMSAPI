using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Published Health Wiki articles only — DRAFT / IN_REVIEW rows never leave the database through
    /// this handler. Newest first. The table is small, so no cache layer (Doctor Dekho caches itself).
    /// </summary>
    public class GetPublicHealthArticlesHandler : IRequestHandler<GetPublicHealthArticlesRequestModel, GetPublicHealthArticlesResponseModel>
    {
        private const int MaxPageSize = 500;

        private readonly AppDbContext _context;

        public GetPublicHealthArticlesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicHealthArticlesResponseModel> Handle(GetPublicHealthArticlesRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.HealthArticles
                .AsNoTracking()
                .Where(a => a.Status == HealthArticle.StatusPublished);

            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var slug = request.Slug.Trim().ToLowerInvariant();
                query = query.Where(a => a.Slug == slug);
            }

            var pageSize = request.PageSize < 1 ? 100 : Math.Min(request.PageSize, MaxPageSize);

            var articles = await query
                .OrderByDescending(a => a.PublishedAt)
                .ThenBy(a => a.Slug)
                .Take(pageSize)
                .Select(a => new PublicHealthArticleInfo
                {
                    Id = a.Slug,
                    Title = a.Title,
                    Description = a.Description,
                    Content = a.Content,
                    RelatedConditionSlug = a.RelatedConditionSlug,
                    AuthorDoctorId = a.AuthorDoctorId,
                    ReviewerDoctorId = a.ReviewerDoctorId,
                    PublishedAt = a.PublishedAt,
                    UpdatedAt = a.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            return new GetPublicHealthArticlesResponseModel { Articles = articles };
        }
    }
}
