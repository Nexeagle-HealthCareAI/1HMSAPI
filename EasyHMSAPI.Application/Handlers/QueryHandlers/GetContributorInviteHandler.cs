using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>What the invitation page shows before the person signs in: who invited them, for what, and the masked number.</summary>
    public class GetContributorInviteHandler : IRequestHandler<GetContributorInviteRequestModel, ContributorInviteInfo>
    {
        private readonly AppDbContext _context;

        public GetContributorInviteHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ContributorInviteInfo> Handle(GetContributorInviteRequestModel request, CancellationToken ct)
        {
            var (link, status, message) = await ContributorLinkCheck.Resolve(_context, request.Token, ct);
            if (link == null) return new ContributorInviteInfo { Success = false, StatusCode = status, Message = message };

            var contributor = link.ContributorId == null ? null
                : await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == link.ContributorId, ct);
            var article = link.ArticleId == null ? null
                : await _context.HealthArticles.AsNoTracking().FirstOrDefaultAsync(a => a.ArticleId == link.ArticleId, ct);

            // The page offers three kinds of enrolment; a staff row (or any other type) is shown as a writer.
            var role = contributor?.Type is HealthWikiContributor.TypeIndependentDoctor or HealthWikiContributor.TypeHealthWorker or HealthWikiContributor.TypeWriter
                ? contributor.Type
                : contributor != null && HealthArticleRules.IsDoctorType(contributor.Type) ? HealthWikiContributor.TypeIndependentDoctor : HealthWikiContributor.TypeWriter;

            return new ContributorInviteInfo
            {
                Success = true,
                StatusCode = 200,
                Kind = link.Role,
                Role = role,
                InviterName = string.IsNullOrWhiteSpace(link.CreatedByName) ? "NexEagle team" : link.CreatedByName,
                MaskedMobile = ContributorSecurity.MaskMobile(link.Mobile),
                ArticleTitle = article?.Title,
                ArticleCoverUrl = article?.CoverImageUrl,
                InviteeName = string.IsNullOrWhiteSpace(link.InviteeName) ? null : link.InviteeName,
            };
        }
    }

    /// <summary>Contributors for the CMS. Rows that never finished joining (no name yet) are left out.</summary>
    public class GetContributorsAdminHandler : IRequestHandler<GetContributorsAdminRequestModel, GetContributorsAdminResponseModel>
    {
        private readonly AppDbContext _context;

        public GetContributorsAdminHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetContributorsAdminResponseModel> Handle(GetContributorsAdminRequestModel request, CancellationToken ct)
        {
            var query = _context.HealthWikiContributors.AsNoTracking().Where(c => c.FullName != string.Empty);
            var status = request.Status?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);
            var type = request.Type?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(type)) query = query.Where(c => c.Type == type);

            var rows = await query.OrderByDescending(c => c.CreatedAt).Take(1000).ToListAsync(ct);
            return new GetContributorsAdminResponseModel { Contributors = rows.Select(ContributorInvites.ToAdminInfo).ToList() };
        }
    }
}
