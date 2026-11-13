using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class HealthWikiMaintenanceService : IHealthWikiMaintenanceService
    {
        public const int ReminderAfterDays = 3;
        public const int MaxReminders = 3;
        public const int CloseNeedsDetailAfterDays = 30;
        public const int KeepOtpDays = 2;
        public const int KeepSessionDays = 30;
        private const int Batch = 500;

        public const string AutoCloseNote = "There was no reply within 30 days, so this request was closed. You are welcome to suggest it again.";

        private readonly AppDbContext _context;
        private readonly IHealthWikiNotifier _notifier;
        private readonly ILogger<HealthWikiMaintenanceService> _logger;

        public HealthWikiMaintenanceService(AppDbContext context, IHealthWikiNotifier notifier, ILogger<HealthWikiMaintenanceService> logger)
        {
            _context = context;
            _notifier = notifier;
            _logger = logger;
        }

        public async Task<HealthWikiMaintenanceResult> RunAsync(CancellationToken ct)
        {
            var result = new HealthWikiMaintenanceResult();
            await Step("review reminders", () => RemindDoctorsAsync(result, ct));
            await Step("closing stale topic requests", () => CloseStaleTopicsAsync(result, ct));
            await Step("clearing old sign-in records", () => ClearOldSignInRecordsAsync(result, ct));
            return result;
        }

        private async Task Step(string name, Func<Task> step)
        {
            try
            {
                await step();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health Wiki upkeep step failed: {Step}", name);
                _context.ChangeTracker.Clear();
            }
        }

        // ------------------------------------------------------------------------------------------ N6

        private async Task RemindDoctorsAsync(HealthWikiMaintenanceResult result, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-ReminderAfterDays);

            // Medical articles with a doctor assigned that have waited for them: a new article, or an edit to a live one.
            var waiting = await _context.HealthArticles.AsNoTracking()
                .Where(a => a.Type == HealthArticle.TypeMedical && a.ReviewerContributorId != null && a.SubmittedAt != null && a.SubmittedAt <= cutoff
                    && ((a.Status == HealthArticle.StatusInReview && a.ApprovedAt == null)
                        || (a.Status == HealthArticle.StatusPublished
                            && _context.HealthArticleRevisions.Any(r => r.ArticleId == a.ArticleId && r.Status == HealthArticleRevision.StatusInReview))))
                .OrderBy(a => a.SubmittedAt).Take(Batch).ToListAsync(ct);

            foreach (var a in waiting)
            {
                var reviewerId = a.ReviewerContributorId!.Value;
                var told = await _context.HealthWikiNotifications.AsNoTracking()
                    .Where(n => n.EntityType == HealthWikiAudit.EntityArticle && n.EntityId == a.ArticleId && n.RecipientContributorId == reviewerId
                        && (n.Code == HealthWikiConstants.Code.ReviewRequest || n.Code == HealthWikiConstants.Code.ReviewReminder))
                    .Select(n => new { n.Code, n.CreatedAt }).ToListAsync(ct);

                // Not more often than every 3 days, and not for ever: after 3 reminders it is the team's job to reassign.
                if (told.Count(n => n.Code == HealthWikiConstants.Code.ReviewReminder) >= MaxReminders) continue;
                if (told.Count > 0 && told.Max(n => n.CreatedAt) > cutoff) continue;

                var edit = a.Status == HealthArticle.StatusPublished
                    ? await _context.HealthArticleRevisions.AsNoTracking().Where(r => r.ArticleId == a.ArticleId && r.Status == HealthArticleRevision.StatusInReview).FirstOrDefaultAsync(ct)
                    : null;
                var days = Math.Max(ReminderAfterDays, (int)(now - a.SubmittedAt!.Value).TotalDays);

                var sent = await _notifier.ReviewReminderAsync(new HealthArticleRef(a.ArticleId, a.Slug, edit?.Title ?? a.Title), reviewerId, days, ct);
                if (sent) result.RemindersSent++; else result.RemindersNotSent++;
            }
        }

        // ------------------------------------------------------------------------------------------ "needs detail" with no reply

        private async Task CloseStaleTopicsAsync(HealthWikiMaintenanceResult result, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddDays(-CloseNeedsDetailAfterDays);
            var stale = await _context.HealthWikiTopicRequests
                .Where(t => t.Status == HealthWikiTopicRequest.StatusNeedsDetail && t.UpdatedAt <= cutoff)
                .OrderBy(t => t.UpdatedAt).Take(Batch).ToListAsync(ct);

            foreach (var t in stale)
            {
                t.Status = HealthWikiTopicRequest.StatusDeclined;
                t.DecisionNote = AutoCloseNote;
                t.DecidedByName = "System";
                t.UpdatedAt = now;
                HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityTopic, t.TopicId, "DECLINED", HealthWikiAudit.ActorSystem, "System", AutoCloseNote);
                await _context.SaveChangesAsync(ct);
                result.TopicsClosed++;
                await _notifier.TopicDeclinedAsync(t, ct);
            }
        }

        // ------------------------------------------------------------------------------------------ housekeeping

        private async Task ClearOldSignInRecordsAsync(HealthWikiMaintenanceResult result, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var oldCodes = await _context.ContributorOtps.Where(o => o.CreatedAt < now.AddDays(-KeepOtpDays)).Take(Batch).ToListAsync(ct);
            _context.ContributorOtps.RemoveRange(oldCodes);
            // The one-time codes that pass a hospital doctor from EasyHMS to Doctor Dekho are sign-in records too (counted with the codes).
            var oldHandoffs = await _context.ContributorHandoffs.Where(h => h.CreatedAt < now.AddDays(-KeepOtpDays)).Take(Batch).ToListAsync(ct);
            _context.ContributorHandoffs.RemoveRange(oldHandoffs);
            result.OtpsRemoved = oldCodes.Count + oldHandoffs.Count;

            var oldSessions = await _context.ContributorSessions
                .Where(s => s.ExpiresAt < now.AddDays(-KeepSessionDays) || (s.RevokedAt != null && s.RevokedAt < now.AddDays(-KeepSessionDays)))
                .Take(Batch).ToListAsync(ct);
            _context.ContributorSessions.RemoveRange(oldSessions);
            result.SessionsRemoved = oldSessions.Count;

            await _context.SaveChangesAsync(ct);
        }
    }
}
