using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class HealthWikiTopicService : IHealthWikiTopicService
    {
        private readonly AppDbContext _context;

        public HealthWikiTopicService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetContributorTopicsResponseModel> ListAsync(Guid contributorId, CancellationToken ct)
        {
            var topics = await _context.HealthWikiTopicRequests.AsNoTracking().Where(t => t.ContributorId == contributorId)
                .OrderByDescending(t => t.UpdatedAt).Take(200).ToListAsync(ct);
            var result = new List<MyTopicInfo>();
            foreach (var t in topics) result.Add(ContributorPortalMapper.ToMyTopic(t, await TopicRules.ArticleSlug(_context, t.ArticleId, ct)));
            return GetContributorTopicsResponseModel.Ok(result);
        }

        public async Task<ContributorTopicResponseModel> CreateAsync(CreateContributorTopicRequestModel r, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == r.ContributorId, ct);
            if (me == null) return ContributorTopicResponseModel.Fail(404, "Profile not found.");
            if (!ContributorPortalRules.IsProfileAccepted(me.Status))
                return ContributorTopicResponseModel.Fail(403, "Your profile has not been accepted yet. Finish registering first.");

            var title = TopicRules.Clean(r.Title);
            var outline = TopicRules.Clean(r.Outline);
            var why = TopicRules.Clean(r.WhyItMatters);
            if (title == null || outline == null || why == null)
                return ContributorTopicResponseModel.Fail(400, "Fill in the topic, the outline and why it matters.");
            if (title.Length > TopicRules.MaxTitle) return ContributorTopicResponseModel.Fail(400, $"The topic is too long (max {TopicRules.MaxTitle} characters).");
            var invalid = TopicRules.Validate(outline, why, r.ConditionSlug, r.References);
            if (invalid != null) return ContributorTopicResponseModel.Fail(400, invalid);

            var type = HealthArticleRules.NormalizeType(r.Type) ?? HealthArticle.TypeMedical;
            if (!HealthArticleRules.CanAuthor(type, me.Type))
                return ContributorTopicResponseModel.Fail(403, "You can suggest sector updates. Medical topics are for doctors.");

            var open = await _context.HealthWikiTopicRequests.CountAsync(t => t.ContributorId == me.ContributorId
                && (t.Status == HealthWikiTopicRequest.StatusSubmitted || t.Status == HealthWikiTopicRequest.StatusNeedsDetail), ct);
            if (open >= ContributorPortalRules.MaxOpenTopics)
                return ContributorTopicResponseModel.Fail(409, $"You already have {ContributorPortalRules.MaxOpenTopics} open requests. Wait for a decision before sending another.");

            var now = DateTime.UtcNow;
            var topic = new HealthWikiTopicRequest
            {
                TopicId = Guid.NewGuid(), ContributorId = me.ContributorId, Title = title, Type = type, Outline = outline, WhyItMatters = why,
                ConditionSlug = TopicRules.Clean(r.ConditionSlug)?.ToLowerInvariant(), References = TopicRules.Clean(r.References),
                Status = HealthWikiTopicRequest.StatusSubmitted, CreatedAt = now, UpdatedAt = now,
                // Only a doctor can ask for someone else to write it.
                RequestWriter = r.RequestWriter && HealthArticleRules.IsDoctorType(me.Type),
            };
            _context.HealthWikiTopicRequests.Add(topic);
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityTopic, topic.TopicId, "SUBMITTED", HealthWikiAudit.ActorContributor, me.FullName, title);
            await _context.SaveChangesAsync(ct);
            return ContributorTopicResponseModel.Ok(ContributorPortalMapper.ToMyTopic(topic, null), 201);
        }

        public async Task<ContributorTopicResponseModel> AddDetailAsync(AddContributorTopicDetailRequestModel r, CancellationToken ct)
        {
            var topic = await _context.HealthWikiTopicRequests.FirstOrDefaultAsync(t => t.TopicId == r.TopicId && t.ContributorId == r.ContributorId, ct);
            if (topic == null) return ContributorTopicResponseModel.Fail(404, "Topic not found.");
            if (topic.Status != HealthWikiTopicRequest.StatusNeedsDetail)
                return ContributorTopicResponseModel.Fail(409, "The team has not asked for more detail on this one.");

            var outline = r.Provided.Contains("outline") ? TopicRules.Clean(r.Outline) : topic.Outline;
            var why = r.Provided.Contains("whyItMatters") ? TopicRules.Clean(r.WhyItMatters) : topic.WhyItMatters;
            var condition = r.Provided.Contains("conditionSlug") ? TopicRules.Clean(r.ConditionSlug)?.ToLowerInvariant() : topic.ConditionSlug;
            var references = r.Provided.Contains("references") ? TopicRules.Clean(r.References) : topic.References;
            if (outline == null || why == null) return ContributorTopicResponseModel.Fail(400, "The outline and why it matters cannot be empty.");
            var invalid = TopicRules.Validate(outline, why, condition, references);
            if (invalid != null) return ContributorTopicResponseModel.Fail(400, invalid);

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
            return ContributorTopicResponseModel.Ok(ContributorPortalMapper.ToMyTopic(topic, await TopicRules.ArticleSlug(_context, topic.ArticleId, ct)));
        }
    }
}
