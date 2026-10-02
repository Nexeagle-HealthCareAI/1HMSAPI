using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Create / partially update a Health Wiki article. PublishedAt is stamped the first time the
    /// status becomes PUBLISHED, kept on later edits, and cleared if the article is pulled back to
    /// DRAFT / IN_REVIEW. Author / reviewer must be existing doctors.
    /// </summary>
    public class SaveHealthArticleHandler : IRequestHandler<SaveHealthArticleRequestModel, SaveHealthArticleResponseModel>
    {
        private readonly AppDbContext _context;

        public SaveHealthArticleHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SaveHealthArticleResponseModel> Handle(SaveHealthArticleRequestModel request, CancellationToken cancellationToken)
        {
            var slug = request.Slug?.Trim().ToLowerInvariant();
            if (!HealthArticleRules.IsValidSlug(slug))
                return Fail(400, "slug must be lowercase letters/digits separated by single hyphens (max 200 chars).");

            string? status = null;
            if (request.Status != null)
            {
                status = HealthArticleRules.NormalizeStatus(request.Status);
                if (status == null)
                    return Fail(400, "status must be DRAFT, IN_REVIEW or PUBLISHED.");
            }

            if (request.Title != null && (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > HealthArticleRules.MaxTitleLength))
                return Fail(400, $"title is required (max {HealthArticleRules.MaxTitleLength} chars).");
            if (request.Description != null && request.Description.Length > HealthArticleRules.MaxDescriptionLength)
                return Fail(400, $"description is max {HealthArticleRules.MaxDescriptionLength} chars.");
            if (request.Content != null && string.IsNullOrWhiteSpace(request.Content))
                return Fail(400, "content cannot be empty.");

            foreach (var doctorId in new[] { request.AuthorDoctorId, request.ReviewerDoctorId })
            {
                if (doctorId.HasValue && !await _context.Doctors.AnyAsync(d => d.DoctorID == doctorId.Value, cancellationToken))
                    return Fail(400, $"doctor {doctorId.Value} not found.");
            }

            var article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug, cancellationToken);
            var now = DateTime.UtcNow;

            if (request.IsCreate)
            {
                if (article != null)
                    return Fail(409, "An article with this slug already exists.");
                if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
                    return Fail(400, "title and content are required.");

                article = new HealthArticle
                {
                    ArticleId = Guid.NewGuid(),
                    Slug = slug!,
                    Title = request.Title.Trim(),
                    Content = request.Content,
                    CreatedAt = now,
                };
                _context.HealthArticles.Add(article);
            }
            else if (article == null)
            {
                return Fail(404, "Article not found.");
            }

            if (!request.IsCreate && request.Title != null) article.Title = request.Title.Trim();
            if (!request.IsCreate && request.Content != null) article.Content = request.Content;
            if (request.Description != null) article.Description = request.Description.Trim();
            if (request.RelatedConditionSlug != null) article.RelatedConditionSlug = request.RelatedConditionSlug.Trim();
            if (request.AuthorDoctorId.HasValue) article.AuthorDoctorId = request.AuthorDoctorId;
            if (request.ReviewerDoctorId.HasValue) article.ReviewerDoctorId = request.ReviewerDoctorId;

            article.Status = status ?? (request.IsCreate ? HealthArticle.StatusDraft : article.Status);
            if (article.Status == HealthArticle.StatusPublished)
                article.PublishedAt ??= now;
            else
                article.PublishedAt = null;

            article.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            return new SaveHealthArticleResponseModel
            {
                Success = true,
                StatusCode = request.IsCreate ? 201 : 200,
                Slug = article.Slug,
                Status = article.Status,
                PublishedAt = article.PublishedAt,
            };
        }

        private static SaveHealthArticleResponseModel Fail(int statusCode, string message) =>
            new() { Success = false, StatusCode = statusCode, Message = message };
    }
}
