using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Services
{
    public sealed class InviteLinkResult
    {
        public bool Created { get; init; }
        public string? Error { get; init; }
        public string? Url { get; init; }
        public bool Delivered { get; init; }
    }

    /// <summary>
    /// Creates a single-use invitation link for a contributor and sends it over WhatsApp. An older unused link for the
    /// same person, article and role is revoked first, so only the newest link works. The raw token exists only in the
    /// returned URL (and the WhatsApp message): the database keeps its hash.
    /// </summary>
    public static class ContributorInvites
    {
        public static string? BaseUrl(IConfiguration configuration)
        {
            var v = configuration["HealthWiki:ContributorBaseUrl"]?.Trim().TrimEnd('/');
            // The committed placeholder must never act as a real address.
            return string.IsNullOrEmpty(v) || v.StartsWith("<") ? null : v;
        }

        public static async Task<InviteLinkResult> CreateAndSendAsync(
            AppDbContext context, IWhatsAppMessagingService whatsApp, IConfiguration configuration,
            HealthWikiContributor contributor, string role, HealthArticle? article, string actor, CancellationToken ct)
        {
            var baseUrl = BaseUrl(configuration);
            if (baseUrl == null)
                return new InviteLinkResult { Error = "HealthWiki:ContributorBaseUrl is not configured, so a link cannot be built." };
            if (string.IsNullOrEmpty(contributor.Mobile))
                return new InviteLinkResult { Error = "This contributor has no mobile number." };

            var now = DateTime.UtcNow;
            var articleId = article?.ArticleId;

            var stale = await context.HealthArticleAccessLinks
                .Where(l => l.ContributorId == contributor.ContributorId && l.Role == role && l.ArticleId == articleId
                            && l.UsedAt == null && l.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var l in stale) l.RevokedAt = now;

            var token = ContributorSecurity.NewToken();
            var link = new HealthArticleAccessLink
            {
                LinkId = Guid.NewGuid(),
                TokenHash = ContributorSecurity.HashToken(token),
                Role = role,
                ArticleId = articleId,
                ContributorId = contributor.ContributorId,
                InviteeName = string.IsNullOrWhiteSpace(contributor.FullName) ? null : contributor.FullName,
                Mobile = contributor.Mobile,
                CreatedByName = actor,
                ExpiresAt = now + ContributorSecurity.LinkLifetime,
                CreatedAt = now,
            };
            context.HealthArticleAccessLinks.Add(link);

            var url = $"{baseUrl}/contribute/invite/{token}";
            var task = role switch
            {
                HealthArticleAccessLink.RoleReview => $"review the article \"{article?.Title}\"",
                HealthArticleAccessLink.RoleWrite => $"write the article \"{article?.Title}\"",
                _ => "contribute to the NexEagle Health Wiki",
            };
            var delivered = await whatsApp.SendHealthWikiInviteAsync(contributor.Mobile, contributor.FullName, task, url);
            if (delivered)
            {
                link.SendCount = 1;
                link.LastSentAt = now;
                contributor.LinkSentAt = now;
            }
            contributor.UpdatedAt = now;

            HealthWikiAuditLog.Add(context, HealthWikiAudit.EntityContributor, contributor.ContributorId,
                delivered ? "LINK_SENT" : "LINK_CREATED", HealthWikiAudit.ActorCmsUser, actor,
                article != null ? $"{role} link for {article.Slug}" : $"{role} link");
            if (article != null)
                HealthWikiAuditLog.Add(context, HealthWikiAudit.EntityArticle, article.ArticleId,
                    delivered ? "LINK_SENT" : "LINK_CREATED", HealthWikiAudit.ActorCmsUser, actor, $"{role} link to {contributor.FullName}");

            await context.SaveChangesAsync(ct);
            return new InviteLinkResult { Created = true, Url = url, Delivered = delivered };
        }

        public static ResponseModels.QueryResponseModels.ContributorAdminInfo ToAdminInfo(HealthWikiContributor c) => new()
        {
            ContributorId = c.ContributorId, Type = c.Type, FullName = c.FullName, Speciality = c.Speciality, Qualification = c.Qualification,
            RoleTitle = c.RoleTitle, Organisation = c.Organisation, Mobile = c.Mobile, RegistrationNumber = c.RegistrationNumber,
            RegistrationCouncil = c.RegistrationCouncil, Status = c.Status, EnrolmentSource = c.EnrolmentSource, LinkSentAt = c.LinkSentAt,
            RejectReason = c.RejectReason,
        };
    }
}
