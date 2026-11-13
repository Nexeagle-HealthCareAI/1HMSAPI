using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    internal static class TopicRules
    {
        public const int MaxTitle = 200, MaxOutline = 2000, MaxWhy = 1000, MaxReferences = 2000;

        public static async Task<string?> ArticleSlug(AppDbContext context, Guid? articleId, CancellationToken ct) =>
            articleId == null ? null : await context.HealthArticles.AsNoTracking().Where(a => a.ArticleId == articleId).Select(a => a.Slug).FirstOrDefaultAsync(ct);

        /// <summary>First problem with the free-text fields, or null.</summary>
        public static string? Validate(string? outline, string? why, string? conditionSlug, string? references)
        {
            if (outline != null && outline.Trim().Length > MaxOutline) return $"The outline is too long (max {MaxOutline} characters).";
            if (why != null && why.Trim().Length > MaxWhy) return $"\"Why it matters\" is too long (max {MaxWhy} characters).";
            if (references != null && references.Trim().Length > MaxReferences) return $"The references are too long (max {MaxReferences} characters).";
            if (!string.IsNullOrWhiteSpace(conditionSlug) && !HealthArticleRules.IsValidSlug(conditionSlug.Trim().ToLowerInvariant()))
                return "The condition is not valid.";
            return null;
        }

        public static string? Clean(string? v) { var t = v?.Trim(); return string.IsNullOrEmpty(t) ? null : t; }
    }

    public class GetContributorTopicsHandler : IRequestHandler<GetContributorTopicsRequestModel, GetContributorTopicsResponseModel>
    {
        private readonly IHealthWikiTopicService _topics;

        public GetContributorTopicsHandler(IHealthWikiTopicService topics)
        {
            _topics = topics;
        }

        public Task<GetContributorTopicsResponseModel> Handle(GetContributorTopicsRequestModel request, CancellationToken ct) => _topics.ListAsync(request.ContributorId, ct);
    }

    /// <summary>
    /// A contributor suggests a topic. Needs an accepted profile, all three of topic, outline and why it matters,
    /// at most 5 open requests (SUBMITTED or NEEDS_DETAIL), and only what the person may write about
    /// (a health worker or writer suggests sector updates).
    /// </summary>
    public class CreateContributorTopicHandler : IRequestHandler<CreateContributorTopicRequestModel, ContributorTopicResponseModel>
    {
        private readonly IHealthWikiTopicService _topics;

        public CreateContributorTopicHandler(IHealthWikiTopicService topics)
        {
            _topics = topics;
        }

        public Task<ContributorTopicResponseModel> Handle(CreateContributorTopicRequestModel r, CancellationToken ct) => _topics.CreateAsync(r, ct);
    }

    /// <summary>The contributor answers "needs detail": the changed fields are saved and the request goes back to the team as SUBMITTED.</summary>
    public class AddContributorTopicDetailHandler : IRequestHandler<AddContributorTopicDetailRequestModel, ContributorTopicResponseModel>
    {
        private readonly IHealthWikiTopicService _topics;

        public AddContributorTopicDetailHandler(IHealthWikiTopicService topics)
        {
            _topics = topics;
        }

        public Task<ContributorTopicResponseModel> Handle(AddContributorTopicDetailRequestModel r, CancellationToken ct) => _topics.AddDetailAsync(r, ct);
    }

    public class GetTopicRequestsAdminHandler : IRequestHandler<GetTopicRequestsAdminRequestModel, GetTopicRequestsAdminResponseModel>
    {
        private readonly AppDbContext _context;

        public GetTopicRequestsAdminHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetTopicRequestsAdminResponseModel> Handle(GetTopicRequestsAdminRequestModel request, CancellationToken ct)
        {
            var query = _context.HealthWikiTopicRequests.AsNoTracking().AsQueryable();
            if (request.TopicId.HasValue) query = query.Where(t => t.TopicId == request.TopicId.Value);
            var status = request.Status?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status == status);

            var topics = await query.OrderByDescending(t => t.UpdatedAt).Take(500).ToListAsync(ct);
            var result = new List<TopicRequestAdminInfo>();
            foreach (var t in topics) result.Add(ContributorPortalMapper.ToAdminTopic(t, await TopicRules.ArticleSlug(_context, t.ArticleId, ct)));
            if (request.TopicId.HasValue && result.Count == 0) return GetTopicRequestsAdminResponseModel.Fail(404, "Topic request not found.");
            return GetTopicRequestsAdminResponseModel.Ok(result);
        }
    }

    /// <summary>
    /// The team's decision on a SUBMITTED topic. ACCEPT creates a draft for the contributor to write (the text starts as the outline);
    /// DECLINE needs a reason; ASK_DETAIL needs a message and puts the request back with the contributor. A decided request is final
    /// until the contributor answers a "needs detail".
    /// </summary>
    public class DecideTopicHandler : IRequestHandler<DecideTopicRequestModel, DecideTopicResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IHealthWikiNotifier _notifier;

        public DecideTopicHandler(AppDbContext context, IHealthWikiNotifier notifier)
        {
            _context = context;
            _notifier = notifier;
        }

        public async Task<DecideTopicResponseModel> Handle(DecideTopicRequestModel r, CancellationToken ct)
        {
            var topic = await _context.HealthWikiTopicRequests.FirstOrDefaultAsync(t => t.TopicId == r.TopicId, ct);
            if (topic == null) return DecideTopicResponseModel.Fail(404, "Topic request not found.");
            if (topic.Status != HealthWikiTopicRequest.StatusSubmitted)
                return DecideTopicResponseModel.Fail(409, "This request is already decided.");

            var actor = HealthWikiAuditLog.ActorOrDefault(r.ActorName);
            var note = r.Note?.Trim();
            var now = DateTime.UtcNow;
            string? articleSlug = null;

            switch (r.Action)
            {
                case DecideTopicRequestModel.Accept:
                {
                    var suggester = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == topic.ContributorId, ct);
                    if (suggester == null || suggester.Status == HealthWikiContributor.StatusRejected)
                        return DecideTopicResponseModel.Fail(409, "This contributor can no longer write articles.");

                    // Normally the person who suggested it writes it. A doctor who asked for a writer gets a draft written by the person the team
                    // chooses (or the content team in the CMS) and becomes its reviewer.
                    Guid? authorId = topic.ContributorId;
                    Guid? reviewerId = null;
                    if (topic.RequestWriter)
                    {
                        if (!HealthArticleRules.IsDoctorType(suggester.Type))
                            return DecideTopicResponseModel.Fail(409, "Only a doctor can ask for a writer.");
                        if (r.ContentTeam)
                        {
                            authorId = null;
                        }
                        else if (r.WriterContributorId.HasValue)
                        {
                            var writer = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == r.WriterContributorId.Value, ct);
                            if (writer == null) return DecideTopicResponseModel.Fail(404, "The writer was not found.");
                            if (writer.Status == HealthWikiContributor.StatusRejected || writer.Status == HealthWikiContributor.StatusInvited)
                                return DecideTopicResponseModel.Fail(409, "This person has not finished joining, or can no longer write articles.");
                            if (!HealthArticleRules.CanAuthor(topic.Type, writer.Type))
                                return DecideTopicResponseModel.Fail(409, "This person cannot write this type of article.");
                            authorId = writer.ContributorId;
                        }
                        else
                        {
                            return DecideTopicResponseModel.Fail(400, "Choose who writes it, or choose the NexEagle content team.");
                        }
                        // A sector update has no reviewer; only a medical article is reviewed by the doctor who asked for it.
                        if (topic.Type == HealthArticle.TypeMedical) reviewerId = suggester.ContributorId;
                    }
                    else if (!HealthArticleRules.CanAuthor(topic.Type, suggester.Type))
                    {
                        return DecideTopicResponseModel.Fail(409, "This contributor cannot write this type of article.");
                    }

                    articleSlug = await UniqueSlug(ContributorPortalRules.Slugify(topic.Title), ct);
                    var article = new HealthArticle
                    {
                        ArticleId = Guid.NewGuid(), Slug = articleSlug, Type = topic.Type, Title = topic.Title, Content = $"## {topic.Title}\n\n{topic.Outline}",
                        RelatedConditionSlug = topic.ConditionSlug, References = topic.References, AuthorContributorId = authorId, ReviewerContributorId = reviewerId,
                        Status = HealthArticle.StatusDraft, CreatedAt = now, UpdatedAt = now,
                    };
                    _context.HealthArticles.Add(article);
                    topic.ArticleId = article.ArticleId;
                    topic.Status = HealthWikiTopicRequest.StatusAccepted;
                    topic.DecisionNote = null;
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "CREATED", HealthWikiAudit.ActorCmsUser, actor,
                        topic.RequestWriter ? $"From the topic \"{topic.Title}\", suggested by {suggester.FullName}, who asked for a writer" : $"From the topic \"{topic.Title}\"");
                    break;
                }
                case DecideTopicRequestModel.Decline:
                    if (string.IsNullOrEmpty(note)) return DecideTopicResponseModel.Fail(400, "A reason is required so the contributor understands.");
                    if (note.Length > HealthArticleRules.MaxReasonLength) return DecideTopicResponseModel.Fail(400, $"The reason is too long (max {HealthArticleRules.MaxReasonLength} characters).");
                    topic.Status = HealthWikiTopicRequest.StatusDeclined;
                    topic.DecisionNote = note;
                    break;
                case DecideTopicRequestModel.AskDetail:
                    if (string.IsNullOrEmpty(note)) return DecideTopicResponseModel.Fail(400, "Say what detail you need.");
                    if (note.Length > HealthArticleRules.MaxReasonLength) return DecideTopicResponseModel.Fail(400, $"The message is too long (max {HealthArticleRules.MaxReasonLength} characters).");
                    topic.Status = HealthWikiTopicRequest.StatusNeedsDetail;
                    topic.DecisionNote = note;
                    break;
                default:
                    return DecideTopicResponseModel.Fail(400, "action must be ACCEPT, DECLINE or ASK_DETAIL.");
            }

            topic.DecidedByName = actor;
            topic.UpdatedAt = now;
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityTopic, topic.TopicId, topic.Status, HealthWikiAudit.ActorCmsUser, actor, note);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return DecideTopicResponseModel.Fail(409, "The request was changed at the same moment. Reload it and try again.");
            }
            var slugForAnswer = articleSlug ?? await TopicRules.ArticleSlug(_context, topic.ArticleId, ct);
            // Saved, so the contributor is told what the team decided (N10, N11 or N12).
            switch (topic.Status)
            {
                // The "accepted" message says a draft is waiting for the person to write. When a doctor asked for a writer that is not true for them:
                // they are told when the article is ready for their review (N5).
                case HealthWikiTopicRequest.StatusAccepted when slugForAnswer != null && !topic.RequestWriter:
                    await _notifier.TopicAcceptedAsync(topic, slugForAnswer, ct);
                    break;
                case HealthWikiTopicRequest.StatusDeclined:
                    await _notifier.TopicDeclinedAsync(topic, ct);
                    break;
                case HealthWikiTopicRequest.StatusNeedsDetail:
                    await _notifier.TopicNeedsDetailAsync(topic, ct);
                    break;
            }
            return DecideTopicResponseModel.Ok(ContributorPortalMapper.ToAdminTopic(topic, slugForAnswer));
        }

        private async Task<string> UniqueSlug(string baseSlug, CancellationToken ct)
        {
            var slug = baseSlug;
            for (var i = 2; await _context.HealthArticles.AnyAsync(a => a.Slug == slug, ct); i++)
            {
                var suffix = "-" + i;
                slug = (baseSlug.Length + suffix.Length > HealthArticleRules.MaxSlugLength ? baseSlug[..(HealthArticleRules.MaxSlugLength - suffix.Length)] : baseSlug) + suffix;
            }
            return slug;
        }
    }
}
