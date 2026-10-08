using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Published Health Wiki articles only. DRAFT / IN_REVIEW rows and pending edits never leave the database through
    /// this handler: the text returned is always the live text. Newest first. The table is small, so no cache layer
    /// (Doctor Dekho caches itself). Contributor mobile and email are never selected.
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
                .ToListAsync(cancellationToken);

            var ids = articles.SelectMany(a => new[] { a.AuthorContributorId, a.ReviewerContributorId })
                .Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
            var people = ids.Count == 0
                ? new Dictionary<Guid, PersonRow>()
                : (await _context.HealthWikiContributors.AsNoTracking()
                    .Where(c => ids.Contains(c.ContributorId))
                    .Select(c => new PersonRow
                    {
                        Id = c.ContributorId, FullName = c.FullName, Type = c.Type, Status = c.Status, Speciality = c.Speciality,
                        Qualification = c.Qualification, RoleTitle = c.RoleTitle, Organisation = c.Organisation, PhotoUrl = c.PhotoUrl,
                        RegistrationNumber = c.RegistrationNumber, RegistrationCouncil = c.RegistrationCouncil,
                    })
                    .ToListAsync(cancellationToken)).ToDictionary(p => p.Id);

            return new GetPublicHealthArticlesResponseModel
            {
                Articles = articles.Select(a => new PublicHealthArticleInfo
                {
                    Id = a.Slug,
                    Type = a.Type,
                    Title = a.Title,
                    Description = a.Description,
                    Content = a.Content,
                    CoverImageUrl = a.CoverImageUrl,
                    CoverImageAlt = a.CoverImageAlt,
                    RelatedConditionSlug = a.RelatedConditionSlug,
                    Disclosure = a.Disclosure,
                    References = a.References,
                    Author = ToAuthor(a.AuthorContributorId, people),
                    // A sector update never exposes a reviewer, whatever is stored.
                    Reviewer = a.Type == HealthArticle.TypeMedical ? ToReviewer(a.ReviewerContributorId, people) : null,
                    PublishedAt = a.PublishedAt,
                    UpdatedAt = a.UpdatedAt,
                }).ToList(),
            };
        }

        private static PublicArticlePerson? ToAuthor(Guid? id, Dictionary<Guid, PersonRow> people)
        {
            if (!id.HasValue || !people.TryGetValue(id.Value, out var p)) return null;
            return new PublicArticlePerson
            {
                Id = p.Id, FullName = p.FullName, Type = p.Type, Speciality = p.Speciality, Qualification = p.Qualification,
                RoleTitle = p.RoleTitle, Organisation = p.Organisation, PhotoUrl = p.PhotoUrl,
            };
        }

        private static PublicArticlePerson? ToReviewer(Guid? id, Dictionary<Guid, PersonRow> people)
        {
            if (!id.HasValue || !people.TryGetValue(id.Value, out var p)) return null;
            return new PublicArticlePerson
            {
                Id = p.Id, FullName = p.FullName, Type = p.Type, Speciality = p.Speciality, Qualification = p.Qualification,
                RoleTitle = p.RoleTitle, Organisation = p.Organisation, PhotoUrl = p.PhotoUrl,
                RegistrationNumber = p.RegistrationNumber, RegistrationCouncil = p.RegistrationCouncil,
                IsRegistrationVerified = HealthArticleRules.ShowsReviewerBadge(HealthArticle.TypeMedical, p.Type, p.Status),
            };
        }

        private sealed class PersonRow
        {
            public Guid Id { get; set; }
            public string FullName { get; set; } = null!;
            public string Type { get; set; } = null!;
            public string Status { get; set; } = null!;
            public string? Speciality { get; set; }
            public string? Qualification { get; set; }
            public string? RoleTitle { get; set; }
            public string? Organisation { get; set; }
            public string? PhotoUrl { get; set; }
            public string? RegistrationNumber { get; set; }
            public string? RegistrationCouncil { get; set; }
        }
    }
}
