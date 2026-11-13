using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static EasyHMSAPI.Data.Constants.HealthWikiConstants;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class HealthWikiNotifier : IHealthWikiNotifier
    {
        // The same message to the same person about the same thing, twice within this time, is one message (a double tap, a retried save).
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(2);
        // A slow answer from Meta must never hold up a doctor's approval.
        private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

        private readonly AppDbContext _context;
        private readonly IWhatsAppMessagingService _whatsApp;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthWikiNotifier> _logger;

        public HealthWikiNotifier(AppDbContext context, IWhatsAppMessagingService whatsApp, IConfiguration configuration, ILogger<HealthWikiNotifier> logger)
        {
            _context = context;
            _whatsApp = whatsApp;
            _configuration = configuration;
            _logger = logger;
        }

        private string? DoctorDekho => ContributorInvites.BaseUrl(_configuration);

        private string? EasyHmsWeb
        {
            get
            {
                var v = _configuration["WebApp:BaseUrl"]?.Trim();
                return string.IsNullOrEmpty(v) || v.StartsWith("<") ? null : v;
            }
        }

        // ------------------------------------------------------------------------------------------ contributors

        public Task ProfileApprovedAsync(HealthWikiContributor c, CancellationToken ct) =>
            SendAsync(Code.ProfileApproved, Template.ProfileApproved, c, HealthWikiAudit.EntityContributor, c.ContributorId, "profile approved",
                Params(("contributor_name", c.FullName), ("url", HealthWikiLinks.ContributorHome(DoctorDekho))), ct);

        public Task ProfileRejectedAsync(HealthWikiContributor c, CancellationToken ct) =>
            SendAsync(Code.ProfileRejected, Template.ProfileRejected, c, HealthWikiAudit.EntityContributor, c.ContributorId, "profile not approved",
                Params(("contributor_name", c.FullName), ("reason", c.RejectReason)), ct);

        public Task RegistrationReceivedAsync(HealthWikiContributor c, CancellationToken ct) =>
            SendAsync(Code.RegistrationReceived, Template.RegistrationReceived, c, HealthWikiAudit.EntityContributor, c.ContributorId, "registration received",
                Params(("contributor_name", c.FullName)), ct);

        // ------------------------------------------------------------------------------------------ articles

        public async Task ReviewRequestedAsync(HealthArticleRef a, Guid reviewerId, CancellationToken ct)
        {
            var reviewer = await Load(reviewerId, ct);
            await SendAsync(Code.ReviewRequest, Template.ReviewRequest, reviewer, HealthWikiAudit.EntityArticle, a.ArticleId, "review requested",
                Params(("reviewer_name", reviewer?.FullName), ("article_title", a.Title), ("url", ReviewLink(reviewer, a.Slug))), ct);
        }

        public async Task<bool> ReviewReminderAsync(HealthArticleRef a, Guid reviewerId, int days, CancellationToken ct)
        {
            var reviewer = await Load(reviewerId, ct);
            return await SendAsync(Code.ReviewReminder, Template.ReviewReminder, reviewer, HealthWikiAudit.EntityArticle, a.ArticleId, "review reminder",
                Params(("reviewer_name", reviewer?.FullName), ("article_title", a.Title), ("days", days.ToString()), ("url", ReviewLink(reviewer, a.Slug))), ct);
        }

        public async Task ChangesRequestedAsync(HealthArticleRef a, Guid? authorId, string requestedBy, string comment, bool byEditor, CancellationToken ct)
        {
            var author = await Load(authorId, ct);
            await SendAsync(byEditor ? Code.ReturnedByEditor : Code.ChangesRequested, Template.ChangesRequested, author, HealthWikiAudit.EntityArticle, a.ArticleId,
                "changes requested",
                Params(("author_name", author?.FullName), ("requested_by", requestedBy), ("article_title", a.Title), ("comment", comment),
                    ("url", HealthWikiLinks.Write(DoctorDekho, a.Slug))), ct);
        }

        public async Task PublishedAsync(HealthArticleRef a, Guid? authorId, Guid? reviewerId, CancellationToken ct)
        {
            foreach (var person in await LoadDistinct(ct, authorId, reviewerId))
                await SendAsync(Code.ArticlePublished, Template.ArticlePublished, person, HealthWikiAudit.EntityArticle, a.ArticleId, "article published",
                    Params(("person_name", person.FullName), ("article_title", a.Title), ("url", HealthWikiLinks.PublicArticle(DoctorDekho, a.Slug))), ct);
        }

        public async Task ArticleRemovedAsync(HealthArticleRef a, Guid? authorId, Guid? reviewerId, string reason, CancellationToken ct)
        {
            foreach (var person in await LoadDistinct(ct, authorId, reviewerId))
                await SendAsync(Code.ArticleRemoved, Template.ArticleRemoved, person, HealthWikiAudit.EntityArticle, a.ArticleId, "article taken down",
                    Params(("person_name", person.FullName), ("article_title", a.Title), ("reason", reason)), ct);
        }

        // ------------------------------------------------------------------------------------------ topics

        public async Task TopicAcceptedAsync(HealthWikiTopicRequest t, string articleSlug, CancellationToken ct)
        {
            var who = await Load(t.ContributorId, ct);
            await SendAsync(Code.TopicAccepted, Template.TopicAccepted, who, HealthWikiAudit.EntityTopic, t.TopicId, "topic accepted",
                Params(("contributor_name", who?.FullName), ("topic_title", t.Title), ("url", HealthWikiLinks.Write(DoctorDekho, articleSlug))), ct);
        }

        public async Task TopicDeclinedAsync(HealthWikiTopicRequest t, CancellationToken ct)
        {
            var who = await Load(t.ContributorId, ct);
            await SendAsync(Code.TopicDeclined, Template.TopicDeclined, who, HealthWikiAudit.EntityTopic, t.TopicId, "topic declined",
                Params(("contributor_name", who?.FullName), ("topic_title", t.Title), ("reason", t.DecisionNote)), ct);
        }

        public async Task TopicNeedsDetailAsync(HealthWikiTopicRequest t, CancellationToken ct)
        {
            var who = await Load(t.ContributorId, ct);
            await SendAsync(Code.TopicNeedsDetail, Template.TopicNeedsDetail, who, HealthWikiAudit.EntityTopic, t.TopicId, "topic needs detail",
                Params(("contributor_name", who?.FullName), ("topic_title", t.Title), ("request", t.DecisionNote), ("url", HealthWikiLinks.Topics(DoctorDekho))), ct);
        }

        // ------------------------------------------------------------------------------------------ the one place a message is sent

        // A doctor in a hospital reviews in EasyHMS; an independent doctor on the Doctor Dekho pages.
        private string? ReviewLink(HealthWikiContributor? reviewer, string slug) =>
            reviewer?.Type == HealthWikiContributor.TypeHospitalDoctor
                ? HealthWikiLinks.EasyHmsReview(EasyHmsWeb, slug)
                : HealthWikiLinks.Review(DoctorDekho, slug);

        private static List<KeyValuePair<string, string>> Params(params (string Name, string? Value)[] values) =>
            values.Select(v => new KeyValuePair<string, string>(v.Name, v.Value ?? string.Empty)).ToList();

        private async Task<HealthWikiContributor?> Load(Guid? id, CancellationToken ct) =>
            id == null ? null : await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == id, ct);

        private async Task<List<HealthWikiContributor>> LoadDistinct(CancellationToken ct, params Guid?[] ids)
        {
            var result = new List<HealthWikiContributor>();
            foreach (var id in ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct())
            {
                var c = await Load(id, ct);
                if (c != null) result.Add(c);
            }
            return result;
        }

        // A contributor has their number on the row; a hospital doctor's is on their EasyHMS user, which is where it already is kept.
        private async Task<string?> MobileOf(HealthWikiContributor c, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(c.Mobile)) return c.Mobile;
            if (c.DoctorId == null) return null;
            var raw = await _context.Doctors.AsNoTracking().Where(d => d.DoctorID == c.DoctorId)
                .Join(_context.Users, d => d.UserID, u => u.UserID, (d, u) => u.MobileNumber).FirstOrDefaultAsync(ct);
            return ContributorSecurity.NormalizeMobile(raw);
        }

        private async Task<bool> SendAsync(string code, string template, HealthWikiContributor? to, string entityType, Guid entityId, string label,
            List<KeyValuePair<string, string>> parameters, CancellationToken ct)
        {
            try
            {
                var mobile = to == null ? null : await MobileOf(to, ct);
                var missingLink = parameters.Any(p => p.Key == "url" && string.IsNullOrWhiteSpace(p.Value));
                if (to == null || mobile == null || missingLink)
                {
                    var why = to == null ? "there is no one to send it to" : mobile == null ? "no mobile number on file" : "the page address is not configured";
                    await RecordAsync(code, template, to, null, entityType, entityId, label, HealthWikiNotification.StatusSkipped, $"Not sent: {why}.", ct);
                    return false;
                }

                var since = DateTime.UtcNow - DuplicateWindow;
                if (await _context.HealthWikiNotifications.AnyAsync(n => n.Code == code && n.EntityId == entityId && n.RecipientContributorId == to.ContributorId
                        && n.Status == HealthWikiNotification.StatusSent && n.CreatedAt > since, ct))
                    return true;

                Task<WhatsAppSendResult> send;
                try { send = _whatsApp.SendHealthWikiTemplateTrackedAsync(mobile, template, parameters); }
                catch (Exception ex) { send = Task.FromException<WhatsAppSendResult>(ex); }

                var first = await Task.WhenAny(send, Task.Delay(SendTimeout, ct));
                if (first != send)
                {
                    _ = send.ContinueWith(t => _logger.LogWarning(t.Exception, "A late WhatsApp answer for {Code} failed", code), TaskContinuationOptions.OnlyOnFaulted);
                    await RecordAsync(code, template, to, mobile, entityType, entityId, label, HealthWikiNotification.StatusFailed, "WhatsApp did not answer in time.", ct);
                    return false;
                }

                bool accepted;
                string? messageId = null;
                string? failure = null;
                try
                {
                    var result = await send;
                    accepted = result.Accepted;
                    messageId = result.MessageId;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WhatsApp call for {Code} failed", code);
                    accepted = false;
                    failure = "WhatsApp could not be reached.";
                }
                await RecordAsync(code, template, to, mobile, entityType, entityId, label,
                    accepted ? HealthWikiNotification.StatusSent : HealthWikiNotification.StatusFailed,
                    accepted ? null : failure ?? "WhatsApp did not accept the message (the template may not be approved yet, or the number is not on WhatsApp).", ct, messageId);
                return accepted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health Wiki notification {Code} for {EntityType} {EntityId} failed", code, entityType, entityId);
                return false;
            }
        }

        private async Task RecordAsync(string code, string template, HealthWikiContributor? to, string? mobile, string entityType, Guid entityId, string label,
            string status, string? detail, CancellationToken ct, string? providerMessageId = null)
        {
            _context.HealthWikiNotifications.Add(new HealthWikiNotification
            {
                NotificationId = Guid.NewGuid(), Code = code, TemplateName = template, RecipientContributorId = to?.ContributorId,
                RecipientMobileMasked = mobile == null ? null : ContributorSecurity.MaskMobile(mobile), EntityType = entityType, EntityId = entityId,
                Status = status, Detail = detail, CreatedAt = DateTime.UtcNow, ProviderMessageId = providerMessageId,
            });
            var action = status == HealthWikiNotification.StatusSent ? "NOTIFIED" : status == HealthWikiNotification.StatusFailed ? "NOTIFY_FAILED" : "NOTIFY_SKIPPED";
            HealthWikiAuditLog.Add(_context, entityType, entityId, action, HealthWikiAudit.ActorSystem, "WhatsApp", $"{code} ({label}) to {to?.FullName ?? "nobody"}" + (detail == null ? "" : $": {detail}"));
            await _context.SaveChangesAsync(ct);
        }
    }
}
