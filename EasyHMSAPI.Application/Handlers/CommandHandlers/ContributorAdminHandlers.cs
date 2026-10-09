using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    internal static class ContributorAdmin
    {
        public static ContributorAdminResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    /// <summary>CMS invites someone by name and number. The person is created as INVITED and gets a JOIN link.</summary>
    public class InviteContributorHandler : IRequestHandler<InviteContributorRequestModel, ContributorAdminResponseModel>
    {
        private static readonly string[] Invitable =
        {
            HealthWikiContributor.TypeIndependentDoctor, HealthWikiContributor.TypeHealthWorker, HealthWikiContributor.TypeWriter,
        };

        private readonly AppDbContext _context;
        private readonly IWhatsAppMessagingService _whatsApp;
        private readonly IConfiguration _configuration;

        public InviteContributorHandler(AppDbContext context, IWhatsAppMessagingService whatsApp, IConfiguration configuration)
        {
            _context = context;
            _whatsApp = whatsApp;
            _configuration = configuration;
        }

        public async Task<ContributorAdminResponseModel> Handle(InviteContributorRequestModel request, CancellationToken ct)
        {
            var name = request.FullName?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 200) return ContributorAdmin.Fail(400, "fullName is required (max 200 chars).");
            var mobile = ContributorSecurity.NormalizeMobile(request.Mobile);
            if (mobile == null) return ContributorAdmin.Fail(400, "mobile must be a 10-digit Indian mobile number.");
            var type = request.Type?.Trim().ToUpperInvariant();
            if (type == null || !Invitable.Contains(type)) return ContributorAdmin.Fail(400, "type must be INDEPENDENT_DOCTOR, HEALTH_WORKER or WRITER.");
            if (ContributorInvites.BaseUrl(_configuration) == null)
                return ContributorAdmin.Fail(503, "Invitations are not configured on this environment (HealthWiki:ContributorBaseUrl).");

            if (await _context.HealthWikiContributors.AnyAsync(c => c.Mobile == mobile, ct))
                return ContributorAdmin.Fail(409, "A contributor with this mobile number already exists.");

            var actor = HealthWikiAuditLog.ActorOrDefault(request.ActorName);
            var now = DateTime.UtcNow;
            var contributor = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = type, FullName = name, Mobile = mobile,
                Status = HealthWikiContributor.StatusInvited, EnrolmentSource = "INVITED", CreatedAt = now, UpdatedAt = now,
            };
            _context.HealthWikiContributors.Add(contributor);
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityContributor, contributor.ContributorId, "INVITED", HealthWikiAudit.ActorCmsUser, actor);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return ContributorAdmin.Fail(409, "A contributor with this mobile number already exists.");
            }

            var link = await ContributorInvites.CreateAndSendAsync(_context, _whatsApp, _configuration, contributor, HealthArticleAccessLink.RoleJoin, null, actor, ct);
            return new ContributorAdminResponseModel
            {
                Success = true, StatusCode = 201, Contributor = ContributorInvites.ToAdminInfo(contributor),
                Link = link.Url, LinkDelivered = link.Delivered,
            };
        }
    }

    /// <summary>CMS sends (or resends) a link: for an article it is the review link (the article's reviewer) or the write link (its author).</summary>
    public class SendContributorLinkHandler : IRequestHandler<SendContributorLinkRequestModel, ContributorAdminResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IWhatsAppMessagingService _whatsApp;
        private readonly IConfiguration _configuration;

        public SendContributorLinkHandler(AppDbContext context, IWhatsAppMessagingService whatsApp, IConfiguration configuration)
        {
            _context = context;
            _whatsApp = whatsApp;
            _configuration = configuration;
        }

        public async Task<ContributorAdminResponseModel> Handle(SendContributorLinkRequestModel request, CancellationToken ct)
        {
            var contributor = await _context.HealthWikiContributors.FirstOrDefaultAsync(c => c.ContributorId == request.ContributorId, ct);
            if (contributor == null) return ContributorAdmin.Fail(404, "Contributor not found.");
            if (contributor.Status == HealthWikiContributor.StatusRejected) return ContributorAdmin.Fail(409, "A rejected contributor cannot be sent a link.");
            if (string.IsNullOrEmpty(contributor.Mobile)) return ContributorAdmin.Fail(409, "This contributor has no mobile number (hospital doctors use the EasyHMS app).");

            HealthArticle? article = null;
            var role = HealthArticleAccessLink.RoleJoin;
            if (!string.IsNullOrWhiteSpace(request.ArticleSlug))
            {
                var slug = request.ArticleSlug.Trim().ToLowerInvariant();
                article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug, ct);
                if (article == null) return ContributorAdmin.Fail(404, "Article not found.");
                if (article.ReviewerContributorId == contributor.ContributorId) role = HealthArticleAccessLink.RoleReview;
                else if (article.AuthorContributorId == contributor.ContributorId) role = HealthArticleAccessLink.RoleWrite;
                else return ContributorAdmin.Fail(400, "This contributor is neither the author nor the reviewer of the article. Assign them first.");
            }

            var actor = HealthWikiAuditLog.ActorOrDefault(request.ActorName);
            var link = await ContributorInvites.CreateAndSendAsync(_context, _whatsApp, _configuration, contributor, role, article, actor, ct);
            if (!link.Created) return ContributorAdmin.Fail(503, link.Error ?? "The link could not be created.");
            return new ContributorAdminResponseModel
            {
                Success = true, StatusCode = 200, Contributor = ContributorInvites.ToAdminInfo(contributor),
                Link = link.Url, LinkDelivered = link.Delivered,
            };
        }
    }

    /// <summary>
    /// A doctor is verified once the team has checked the registration number against the council's register;
    /// a health worker or writer is approved. The person must have finished registering first (status PENDING).
    /// </summary>
    public class VerifyContributorHandler : IRequestHandler<VerifyContributorRequestModel, ContributorAdminResponseModel>
    {
        private readonly AppDbContext _context;

        public VerifyContributorHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ContributorAdminResponseModel> Handle(VerifyContributorRequestModel request, CancellationToken ct)
        {
            var c = await _context.HealthWikiContributors.FirstOrDefaultAsync(x => x.ContributorId == request.ContributorId, ct);
            if (c == null) return ContributorAdmin.Fail(404, "Contributor not found.");
            if (c.Status == HealthWikiContributor.StatusVerified) return ContributorAdmin.Fail(409, "Already verified.");
            if (c.Status != HealthWikiContributor.StatusPending)
                return ContributorAdmin.Fail(409, c.Status == HealthWikiContributor.StatusInvited
                    ? "This person has not finished registering yet."
                    : "A rejected contributor cannot be verified.");
            if (HealthArticleRules.IsDoctorType(c.Type) && (string.IsNullOrWhiteSpace(c.RegistrationNumber) || string.IsNullOrWhiteSpace(c.RegistrationCouncil)))
                return ContributorAdmin.Fail(400, "A doctor needs a registration number and council before they can be verified.");

            var actor = HealthWikiAuditLog.ActorOrDefault(request.ActorName);
            var now = DateTime.UtcNow;
            c.Status = HealthWikiContributor.StatusVerified;
            c.RejectReason = null;
            c.VerifiedAt = now;
            c.VerifiedBy = actor;
            c.UpdatedAt = now;
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityContributor, c.ContributorId, "VERIFIED", HealthWikiAudit.ActorCmsUser, actor);
            await _context.SaveChangesAsync(ct);
            return new ContributorAdminResponseModel { Success = true, StatusCode = 200, Contributor = ContributorInvites.ToAdminInfo(c) };
        }
    }

    /// <summary>Reject with a reason. Signs the person out everywhere and cancels their open links.</summary>
    public class RejectContributorHandler : IRequestHandler<RejectContributorRequestModel, ContributorAdminResponseModel>
    {
        private readonly AppDbContext _context;

        public RejectContributorHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ContributorAdminResponseModel> Handle(RejectContributorRequestModel request, CancellationToken ct)
        {
            var reason = request.Reason?.Trim();
            if (string.IsNullOrEmpty(reason)) return ContributorAdmin.Fail(400, "A reason is required.");
            if (reason.Length > HealthArticleRules.MaxReasonLength) return ContributorAdmin.Fail(400, $"reason is max {HealthArticleRules.MaxReasonLength} chars.");

            var c = await _context.HealthWikiContributors.FirstOrDefaultAsync(x => x.ContributorId == request.ContributorId, ct);
            if (c == null) return ContributorAdmin.Fail(404, "Contributor not found.");
            if (c.Status == HealthWikiContributor.StatusRejected) return ContributorAdmin.Fail(409, "Already rejected.");

            var actor = HealthWikiAuditLog.ActorOrDefault(request.ActorName);
            var now = DateTime.UtcNow;
            c.Status = HealthWikiContributor.StatusRejected;
            c.RejectReason = reason;
            c.UpdatedAt = now;

            var sessions = await _context.ContributorSessions.Where(s => s.ContributorId == c.ContributorId && s.RevokedAt == null).ToListAsync(ct);
            foreach (var s in sessions) s.RevokedAt = now;
            var links = await _context.HealthArticleAccessLinks.Where(l => l.ContributorId == c.ContributorId && l.UsedAt == null && l.RevokedAt == null).ToListAsync(ct);
            foreach (var l in links) l.RevokedAt = now;

            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityContributor, c.ContributorId, "REJECTED", HealthWikiAudit.ActorCmsUser, actor, reason);
            await _context.SaveChangesAsync(ct);
            return new ContributorAdminResponseModel { Success = true, StatusCode = 200, Contributor = ContributorInvites.ToAdminInfo(c) };
        }
    }
}
