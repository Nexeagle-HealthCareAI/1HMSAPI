using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
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

    public class GetContributorTopicsHandler : IRequestHandler<GetContributorTopicsRequestModel, ApiResult<List<MyTopicInfo>>>
    {
        private readonly AppDbContext _context;

        public GetContributorTopicsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<List<MyTopicInfo>>> Handle(GetContributorTopicsRequestModel request, CancellationToken ct)
        {
            var topics = await _context.HealthWikiTopicRequests.AsNoTracking().Where(t => t.ContributorId == request.ContributorId)
                .OrderByDescending(t => t.UpdatedAt).Take(200).ToListAsync(ct);
            var result = new List<MyTopicInfo>();
            foreach (var t in topics) result.Add(ContributorPortalMapper.ToMyTopic(t, await TopicRules.ArticleSlug(_context, t.ArticleId, ct)));
            return ApiResult<List<MyTopicInfo>>.Ok(result);
        }
    }

    /// <summary>
    /// A contributor suggests a topic. Needs an accepted profile, all three of topic, outline and why it matters,
    /// at most 5 open requests (SUBMITTED or NEEDS_DETAIL), and only what the person may write about
    /// (a health worker or writer suggests sector updates).
    /// </summary>
    public class CreateContributorTopicHandler : IRequestHandler<CreateContributorTopicRequestModel, ApiResult<MyTopicInfo>>
    {
        private readonly AppDbContext _context;

        public CreateContributorTopicHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<MyTopicInfo>> Handle(CreateContributorTopicRequestModel r, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == r.ContributorId, ct);
            if (me == null) return ApiResult<MyTopicInfo>.Fail(404, "Profile not found.");
            if (!ContributorPortalRules.IsProfileAccepted(me.Status))
                return ApiResult<MyTopicInfo>.Fail(403, "Your profile has not been accepted yet. Finish registering first.");

            var title = TopicRules.Clean(r.Title);
            var outline = TopicRules.Clean(r.Outline);
            var why = TopicRules.Clean(r.WhyItMatters);
            if (title == null || outline == null || why == null)
                return ApiResult<MyTopicInfo>.Fail(400, "Fill in the topic, the outline and why it matters.");
            if (title.Length > TopicRules.MaxTitle) return ApiResult<MyTopicInfo>.Fail(400, $"The topic is too long (max {TopicRules.MaxTitle} characters).");
            var invalid = TopicRules.Validate(outline, why, r.ConditionSlug, r.References);
            if (invalid != null) return ApiResult<MyTopicInfo>.Fail(400, invalid);

            var type = HealthArticleRules.NormalizeType(r.Type) ?? HealthArticle.TypeMedical;
            if (!HealthArticleRules.CanAuthor(type, me.Type))
                return ApiResult<MyTopicInfo>.Fail(403, "You can suggest sector updates. Medical topics are for doctors.");

            var open = await _context.HealthWikiTopicRequests.CountAsync(t => t.ContributorId == me.ContributorId
                && (t.Status == HealthWikiTopicRequest.StatusSubmitted || t.Status == HealthWikiTopicRequest.StatusNeedsDetail), ct);
            if (open >= ContributorPortalRules.MaxOpenTopics)
                return ApiResult<MyTopicInfo>.Fail(409, $"You already have {ContributorPortalRules.MaxOpenTopics} open requests. Wait for a decision before sending another.");

            var now = DateTime.UtcNow;
            var topic = new HealthWikiTopicRequest
            {
                TopicId = Guid.NewGuid(), ContributorId = me.ContributorId, Title = title, Type = type, Outline = outline, WhyItMatters = why,
                ConditionSlug = TopicRules.Clean(r.ConditionSlug)?.ToLowerInvariant(), References = TopicRules.Clean(r.References),
                Status = HealthWikiTopicRequest.StatusSubmitted, CreatedAt = now, UpdatedAt = now,
            };
            _context.HealthWikiTopicRequests.Add(topic);
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityTopic, topic.TopicId, "SUBMITTED", HealthWikiAudit.ActorContributor, me.FullName, title);
            await _context.SaveChangesAsync(ct);
            return ApiResult<MyTopicInfo>.Ok(ContributorPortalMapper.ToMyTopic(topic, null), 201);
        }
    }

    /// <summary>The contributor answers "needs detail": the changed fields are saved and the request goes back to the team as SUBMITTED.</summary>
    public class AddContributorTopicDetailHandler : IRequestHandler<AddContributorTopicDetailRequestModel, ApiResult<MyTopicInfo>>
    {
        private readonly AppDbContext _context;

        public AddContributorTopicDetailHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<MyTopicInfo>> Handle(AddContributorTopicDetailRequestModel r, CancellationToken ct)
        {
            var topic = await _context.HealthWikiTopicRequests.FirstOrDefaultAsync(t => t.TopicId == r.TopicId && t.ContributorId == r.ContributorId, ct);
            if (topic == null) return ApiResult<MyTopicInfo>.Fail(404, "Topic not found.");
            if (topic.Status != HealthWikiTopicRequest.StatusNeedsDetail)
                return ApiResult<MyTopicInfo>.Fail(409, "The team has not asked for more detail on this one.");

            var outline = r.Provided.Contains("outline") ? TopicRules.Clean(r.Outline) : topic.Outline;
            var why = r.Provided.Contains("whyItMatters") ? TopicRules.Clean(r.WhyItMatters) : topic.WhyItMatters;
            var condition = r.Provided.Contains("conditionSlug") ? TopicRules.Clean(r.ConditionSlug)?.ToLowerInvariant() : topic.ConditionSlug;
            var references = r.Provided.Contains("references") ? TopicRules.Clean(r.References) : topic.References;
            if (outline == null || why == null) return ApiResult<MyTopicInfo>.Fail(400, "The outline and why it matters cannot be empty.");
            var invalid = TopicRules.Validate(outline, why, condition, references);
            if (invalid != null) return ApiResult<MyTopicInfo>.Fail(400, invalid);

            topic.Outline = outline;
            topic.WhyItMatters = why;
            topic.ConditionSlug = condition;
            topic.References = references;
            topic.Status = HealthWikiTopicRequest.StatusSubmitted;
            topic.DecisionNote = null;
            topic.UpdatedAt = DateTime.UtcNow;
            var name = await _context.HealthWikiContributors.Where(c => c.ContributorId == r.ContributorId).Select(c => c.FullName).FirstOrDefaultAsync(ct);
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityTopic, topic.TopicId, "DETAIL_ADDED", HealthWikiAudit.ActorContributor, name);
            await _context.SaveChangesAsync(ct);
            return ApiResult<MyTopicInfo>.Ok(ContributorPortalMapper.ToMyTopic(topic, await TopicRules.ArticleSlug(_context, topic.ArticleId, ct)));
        }
    }

    public class GetTopicRequestsAdminHandler : IRequestHandler<GetTopicRequestsAdminRequestModel, ApiResult<List<TopicRequestAdminInfo>>>
    {
        private readonly AppDbContext _context;

        public GetTopicRequestsAdminHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<List<TopicRequestAdminInfo>>> Handle(GetTopicRequestsAdminRequestModel request, CancellationToken ct)
        {
            var query = _context.HealthWikiTopicRequests.AsNoTracking().AsQueryable();
            if (request.TopicId.HasValue) query = query.Where(t => t.TopicId == request.TopicId.Value);
            var status = request.Status?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status == status);

            var topics = await query.OrderByDescending(t => t.UpdatedAt).Take(500).ToListAsync(ct);
            var result = new List<TopicRequestAdminInfo>();
            foreach (var t in topics) result.Add(ContributorPortalMapper.ToAdminTopic(t, await TopicRules.ArticleSlug(_context, t.ArticleId, ct)));
            if (request.TopicId.HasValue && result.Count == 0) return ApiResult<List<TopicRequestAdminInfo>>.Fail(404, "Topic request not found.");
            return ApiResult<List<TopicRequestAdminInfo>>.Ok(result);
        }
    }

    /// <summary>
    /// The team's decision on a SUBMITTED topic. ACCEPT creates a draft for the contributor to write (the text starts as the outline);
    /// DECLINE needs a reason; ASK_DETAIL needs a message and puts the request back with the contributor. A decided request is final
    /// until the contributor answers a "needs detail".
    /// </summary>
    public class DecideTopicHandler : IRequestHandler<DecideTopicRequestModel, ApiResult<TopicRequestAdminInfo>>
    {
        private readonly AppDbContext _context;

        public DecideTopicHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<TopicRequestAdminInfo>> Handle(DecideTopicRequestModel r, CancellationToken ct)
        {
            var topic = await _context.HealthWikiTopicRequests.FirstOrDefaultAsync(t => t.TopicId == r.TopicId, ct);
            if (topic == null) return ApiResult<TopicRequestAdminInfo>.Fail(404, "Topic request not found.");
            if (topic.Status != HealthWikiTopicRequest.StatusSubmitted)
                return ApiResult<TopicRequestAdminInfo>.Fail(409, "This request is already decided.");

            var actor = HealthWikiAuditLog.ActorOrDefault(r.ActorName);
            var note = r.Note?.Trim();
            var now = DateTime.UtcNow;
            string? articleSlug = null;

            switch (r.Action)
            {
                case DecideTopicRequestModel.Accept:
                {
                    var author = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == topic.ContributorId, ct);
                    if (author == null || author.Status == HealthWikiContributor.StatusRejected)
                        return ApiResult<TopicRequestAdminInfo>.Fail(409, "This contributor can no longer write articles.");
                    if (!HealthArticleRules.CanAuthor(topic.Type, author.Type))
                        return ApiResult<TopicRequestAdminInfo>.Fail(409, "This contributor cannot write this type of article.");

                    articleSlug = await UniqueSlug(ContributorPortalRules.Slugify(topic.Title), ct);
                    var article = new HealthArticle
                    {
                        ArticleId = Guid.NewGuid(), Slug = articleSlug, Type = topic.Type, Title = topic.Title, Content = $"## {topic.Title}\n\n{topic.Outline}",
                        RelatedConditionSlug = topic.ConditionSlug, References = topic.References, AuthorContributorId = topic.ContributorId,
                        Status = HealthArticle.StatusDraft, CreatedAt = now, UpdatedAt = now,
                    };
                    _context.HealthArticles.Add(article);
                    topic.ArticleId = article.ArticleId;
                    topic.Status = HealthWikiTopicRequest.StatusAccepted;
                    topic.DecisionNote = null;
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "CREATED", HealthWikiAudit.ActorCmsUser, actor, $"From the topic \"{topic.Title}\"");
                    break;
                }
                case DecideTopicRequestModel.Decline:
                    if (string.IsNullOrEmpty(note)) return ApiResult<TopicRequestAdminInfo>.Fail(400, "A reason is required so the contributor understands.");
                    if (note.Length > HealthArticleRules.MaxReasonLength) return ApiResult<TopicRequestAdminInfo>.Fail(400, $"The reason is too long (max {HealthArticleRules.MaxReasonLength} characters).");
                    topic.Status = HealthWikiTopicRequest.StatusDeclined;
                    topic.DecisionNote = note;
                    break;
                case DecideTopicRequestModel.AskDetail:
                    if (string.IsNullOrEmpty(note)) return ApiResult<TopicRequestAdminInfo>.Fail(400, "Say what detail you need.");
                    if (note.Length > HealthArticleRules.MaxReasonLength) return ApiResult<TopicRequestAdminInfo>.Fail(400, $"The message is too long (max {HealthArticleRules.MaxReasonLength} characters).");
                    topic.Status = HealthWikiTopicRequest.StatusNeedsDetail;
                    topic.DecisionNote = note;
                    break;
                default:
                    return ApiResult<TopicRequestAdminInfo>.Fail(400, "action must be ACCEPT, DECLINE or ASK_DETAIL.");
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
                return ApiResult<TopicRequestAdminInfo>.Fail(409, "The request was changed at the same moment. Reload it and try again.");
            }
            return ApiResult<TopicRequestAdminInfo>.Ok(ContributorPortalMapper.ToAdminTopic(topic, articleSlug ?? await TopicRules.ArticleSlug(_context, topic.ArticleId, ct)));
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
