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
    /// Create / partially update a Health Wiki article (CMS and, later, contributors).
    ///
    /// Trust rules enforced here, not only in the UI:
    ///  - MEDICAL has a doctor reviewer and is published only after that reviewer approved (ApprovedAt) and is VERIFIED.
    ///  - SECTOR_UPDATE never has a reviewer; a CMS editor publishes it.
    ///  - Health workers and writers cannot author MEDICAL articles.
    ///  - Editing a PUBLISHED article never changes the live text: the edit is held as a pending revision
    ///    until it is approved (see DecideHealthArticleHandler).
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

            var actor = HealthWikiAuditLog.ActorOrDefault(request.ActorName);

            string? status = null;
            if (request.Has("status", request.Status))
            {
                status = HealthArticleRules.NormalizeStatus(request.Status);
                if (status == null)
                    return Fail(400, "status must be DRAFT, IN_REVIEW or PUBLISHED.");
            }

            string? requestedType = null;
            if (request.Has("type", request.Type))
            {
                requestedType = HealthArticleRules.NormalizeType(request.Type);
                if (requestedType == null)
                    return Fail(400, "type must be MEDICAL or SECTOR_UPDATE.");
            }

            var article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug, cancellationToken);
            var now = DateTime.UtcNow;
            var isNew = request.IsCreate;

            if (isNew)
            {
                if (article != null)
                    return Fail(409, "An article with this slug already exists.");
                article = new HealthArticle
                {
                    ArticleId = Guid.NewGuid(),
                    Slug = slug!,
                    Type = requestedType ?? HealthArticle.TypeMedical,
                    Status = HealthArticle.StatusDraft,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
            }
            else if (article == null)
            {
                return Fail(404, "Article not found.");
            }

            var wasPublished = !isNew && article.Status == HealthArticle.StatusPublished;
            if (wasPublished && requestedType != null && requestedType != article.Type)
                return Fail(400, "The type of a published article cannot be changed.");
            var type = requestedType ?? article.Type;

            // ---- contributors -------------------------------------------------------------------------------
            var authorId = request.Has("authorContributorId", request.AuthorContributorId) ? request.AuthorContributorId : article.AuthorContributorId;
            var reviewerId = request.Has("reviewerContributorId", request.ReviewerContributorId) ? request.ReviewerContributorId : article.ReviewerContributorId;

            HealthWikiContributor? author = null, reviewer = null;
            if (authorId.HasValue)
            {
                author = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == authorId.Value, cancellationToken);
                if (author == null) return Fail(400, $"author {authorId.Value} not found.");
                if (author.Status == HealthWikiContributor.StatusRejected) return Fail(400, "A rejected contributor cannot be the author.");
                if (!HealthArticleRules.CanAuthor(type, author.Type))
                    return Fail(400, "Only a doctor or NexEagle staff can author a medical article. Health workers and writers write sector updates.");
            }
            if (reviewerId.HasValue)
            {
                if (type == HealthArticle.TypeSectorUpdate)
                    return Fail(400, "A sector update is not doctor-reviewed, so it cannot have a reviewer.");
                reviewer = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == reviewerId.Value, cancellationToken);
                if (reviewer == null) return Fail(400, $"reviewer {reviewerId.Value} not found.");
                if (!HealthArticleRules.CanReview(reviewer.Type, reviewer.Status))
                    return Fail(400, "Only a doctor can review an article.");
            }

            // ---- text fields: build the working copy -----------------------------------------------------------
            HealthArticleRevision? revision = null;
            if (wasPublished)
                revision = await _context.HealthArticleRevisions.FirstOrDefaultAsync(
                    r => r.ArticleId == article.ArticleId && (r.Status == HealthArticleRevision.StatusDraft || r.Status == HealthArticleRevision.StatusInReview),
                    cancellationToken);

            var working = isNew ? new ArticleContent()
                : revision != null ? ArticleContent.From(revision)
                : ArticleContent.From(article);

            var contentChanged = false;
            if (request.Has("title", request.Title)) { working.Title = request.Title?.Trim() ?? string.Empty; contentChanged = true; }
            if (request.Has("description", request.Description)) { working.Description = Clean(request.Description); contentChanged = true; }
            if (request.Has("content", request.Content)) { working.Content = request.Content ?? string.Empty; contentChanged = true; }
            if (request.Has("relatedConditionSlug", request.RelatedConditionSlug)) { working.RelatedConditionSlug = Clean(request.RelatedConditionSlug)?.ToLowerInvariant(); contentChanged = true; }
            if (request.Has("coverImageUrl", request.CoverImageUrl)) { working.CoverImageUrl = Clean(request.CoverImageUrl); contentChanged = true; }
            if (request.Has("coverImageAlt", request.CoverImageAlt)) { working.CoverImageAlt = Clean(request.CoverImageAlt); contentChanged = true; }
            if (request.Has("disclosure", request.Disclosure)) { working.Disclosure = Clean(request.Disclosure); contentChanged = true; }
            if (request.Has("references", request.References)) { working.References = Clean(request.References); contentChanged = true; }

            if (isNew && (string.IsNullOrWhiteSpace(working.Title) || string.IsNullOrWhiteSpace(working.Content)))
                return Fail(400, "title and content are required.");
            if (isNew || contentChanged)
            {
                var problem = HealthArticleRules.ValidateContent(working);
                if (problem != null) return Fail(400, problem);
            }

            var assignedReviewer = reviewerId != article.ReviewerContributorId;

            // ---- published article: the live text stays; the edit becomes a pending revision ------------------------
            if (wasPublished)
            {
                if (status == HealthArticle.StatusDraft)
                    return Fail(400, "A published article cannot be returned to draft by saving. Use withdraw (for a pending edit) or unpublish.");
                if (assignedReviewer && reviewerId.HasValue && reviewer!.Status != HealthWikiContributor.StatusVerified)
                    return Fail(400, "A live medical article can only be reassigned to a verified doctor.");
                if (assignedReviewer && !reviewerId.HasValue && type == HealthArticle.TypeMedical)
                    return Fail(400, "A live medical article must keep a reviewer.");

                if (contentChanged)
                {
                    if (revision == null)
                    {
                        revision = new HealthArticleRevision
                        {
                            RevisionId = Guid.NewGuid(),
                            ArticleId = article.ArticleId,
                            Status = HealthArticleRevision.StatusDraft,
                            EditedByName = actor,
                            CreatedAt = now,
                        };
                        _context.HealthArticleRevisions.Add(revision);
                    }
                    working.CopyTo(revision);
                    revision.Status = HealthArticleRevision.StatusDraft; // an edit after "sent for review" needs sending again
                    revision.ReviewerComment = null;
                    revision.UpdatedAt = now;
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "REVISION_SAVED", HealthWikiAudit.ActorCmsUser, actor);
                }

                if (status == HealthArticle.StatusInReview)
                {
                    if (revision == null) return Fail(400, "There is no edit to send for review.");
                    if (type == HealthArticle.TypeMedical && !reviewerId.HasValue)
                        return Fail(400, "Choose a doctor reviewer before sending a medical article for review.");
                    revision.Status = HealthArticleRevision.StatusInReview;
                    revision.UpdatedAt = now;
                    article.SubmittedAt = now;
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "SUBMITTED", HealthWikiAudit.ActorCmsUser, actor, "Edit of the published article");
                }

                if (request.Has("authorContributorId", request.AuthorContributorId)) article.AuthorContributorId = authorId;
                if (assignedReviewer)
                {
                    article.ReviewerContributorId = reviewerId;
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "REVIEWER_ASSIGNED", HealthWikiAudit.ActorCmsUser, actor, reviewer?.FullName);
                }
            }
            else
            {
                // ---- article not live yet: edit it directly ---------------------------------------------------------------
                working.CopyTo(article);
                article.Type = type;
                article.AuthorContributorId = authorId;
                article.ReviewerContributorId = reviewerId;

                var target = status ?? article.Status;
                var wasInReview = article.Status == HealthArticle.StatusInReview;

                if (target == HealthArticle.StatusInReview)
                {
                    if (type == HealthArticle.TypeMedical && !reviewerId.HasValue)
                        return Fail(400, "Choose a doctor reviewer before sending a medical article for review.");
                    if (!wasInReview)
                    {
                        article.SubmittedAt = now;
                        article.ReviewerComment = null;
                    }
                }
                else if (target == HealthArticle.StatusPublished)
                {
                    if (type == HealthArticle.TypeMedical)
                    {
                        // Only the doctor's own approval publishes a medical article; the CMS cannot override it.
                        if (article.ApprovedAt == null || reviewer == null || reviewer.Status != HealthWikiContributor.StatusVerified)
                            return Fail(400, "A medical article is published only after its verified doctor reviewer approves it.");
                    }
                    else
                    {
                        article.ApprovedAt ??= now;
                        article.ApprovedByName = actor;
                    }
                    article.PublishedAt ??= now;
                    article.ReviewerComment = null;
                }

                if (target != HealthArticle.StatusPublished) article.PublishedAt = null;
                article.Status = target;
                article.UpdatedAt = now;

                if (isNew)
                {
                    _context.HealthArticles.Add(article);
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "CREATED", HealthWikiAudit.ActorCmsUser, actor);
                }
                else if (contentChanged)
                {
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "UPDATED", HealthWikiAudit.ActorCmsUser, actor);
                }
                if (assignedReviewer && reviewerId.HasValue)
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "REVIEWER_ASSIGNED", HealthWikiAudit.ActorCmsUser, actor, reviewer!.FullName);
                if (target == HealthArticle.StatusInReview && !wasInReview)
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "SUBMITTED", HealthWikiAudit.ActorCmsUser, actor);
                if (target == HealthArticle.StatusPublished)
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "PUBLISHED", HealthWikiAudit.ActorCmsUser, actor);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new SaveHealthArticleResponseModel
            {
                Success = true,
                StatusCode = isNew ? 201 : 200,
                Slug = article.Slug,
                Status = article.Status,
                PublishedAt = article.PublishedAt,
                Article = HealthArticleAdminMapper.ToInfo(article, revision),
            };
        }

        // "" and whitespace mean "no value" so the CMS can clear an optional field.
        private static string? Clean(string? value)
        {
            var v = value?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }

        private static SaveHealthArticleResponseModel Fail(int statusCode, string message) =>
            new() { Success = false, StatusCode = statusCode, Message = message };
    }
}
