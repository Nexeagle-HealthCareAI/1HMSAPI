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
            "REVIEWER_ASSIGNED" => "Assigned a reviewer",
            "REVIEWER_APPROVED" => "Reviewer approved",
            "REVIEWER_REQUESTED_CHANGES" => "Reviewer asked for changes",
            _ => action,
        };
    }
}
