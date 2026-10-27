using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>Adds rows to the Health Wiki history. Saved together with the change that caused them.</summary>
    public static class HealthWikiAuditLog
    {
        public const string DefaultActor = "CMS";

        public static string ActorOrDefault(string? actor)
        {
            var a = actor?.Trim();
            if (string.IsNullOrEmpty(a)) return DefaultActor;
            return a.Length > 200 ? a[..200] : a;
        }

        public static void Add(AppDbContext context, string entityType, Guid entityId, string action, string actorType, string? actorName, string? detail = null)
        {
            context.HealthWikiAudits.Add(new HealthWikiAudit
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                ActorType = actorType,
                ActorName = actorName,
                Detail = detail != null && detail.Length > 1000 ? detail[..1000] : detail,
                CreatedAt = DateTime.UtcNow,
            });
        }

        /// <summary>Wording shown in the CMS history panel.</summary>
        public static string Label(string action) => action switch
        {
            "CREATED" => "Created the article",
            "UPDATED" => "Edited the article",
            "REVISION_SAVED" => "Saved an edit to the published article (not live yet)",
            "SUBMITTED" => "Sent for review",
            "PUBLISHED" => "Published",
            "APPROVED" => "Approved and published",
            "REVISION_APPLIED" => "Approved an edit, now live",
            "SENT_BACK" => "Sent back for changes",
            "UNPUBLISHED" => "Taken offline",
            "SELF_PUBLISHED" => "Published by the verified doctor, without a review",
            "PULLED_BACK" => "Pulled back to review",
            "PUT_BACK_LIVE" => "Put back live without a review",
            "REVIEWER_ASSIGNED" => "Assigned a reviewer",
            "REVIEWER_APPROVED" => "Reviewer approved",
            "REVIEWER_REQUESTED_CHANGES" => "Reviewer asked for changes",
            "NOTIFIED" => "Told on WhatsApp",
            "NOTIFY_FAILED" => "Could not tell on WhatsApp",
            "NOTIFY_SKIPPED" => "Not told on WhatsApp",
            _ => action,
        };

        /// <summary>
        /// Wording a doctor sees on their own article (null = not shown to the doctor: WhatsApp delivery notes, and every save of a draft).
        /// </summary>
        public static string? DoctorLabel(string action) => action switch
        {
            "CREATED" => "Draft started",
            "REVISION_SAVED" => "Edit saved, not live yet",
            "SUBMITTED" => "Sent for review",
            "SELF_PUBLISHED" => "Published, no review needed",
            "PUBLISHED" => "Published",
            "APPROVED" => "Approved and published",
            "REVISION_APPLIED" => "Edit is now live",
            "SENT_BACK" => "Sent back for changes",
            "UNPUBLISHED" => "Taken offline",
            "PULLED_BACK" => "Pulled back to review",
            "PUT_BACK_LIVE" => "Put back live",
            "REVIEWER_ASSIGNED" => "A reviewer was assigned",
            "REVIEWER_APPROVED" => "Reviewer approved",
            "REVIEWER_REQUESTED_CHANGES" => "Reviewer asked for changes",
            _ => null,
        };

        /// <summary>The note on these actions is a reason or comment written for the doctor. On the others it is internal wording.</summary>
        public static bool DoctorSeesDetail(string action) =>
            action is "SENT_BACK" or "UNPUBLISHED" or "PULLED_BACK" or "PUT_BACK_LIVE" or "REVIEWER_REQUESTED_CHANGES";
    }
}
