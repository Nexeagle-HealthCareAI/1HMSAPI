using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Conditions an article can be linked to ("Need a specialist?" card). The list comes from configuration
    /// (HealthWiki:Conditions, comma separated) so it can grow without a deploy of code; slugs already used by
    /// articles are always included so no existing link disappears.
    /// </summary>
    public class GetHealthWikiConditionsHandler : IRequestHandler<GetHealthWikiConditionsRequestModel, GetHealthWikiConditionsResponseModel>
    {
        private static readonly string[] Defaults = { "diabetes", "hypertension", "thyroid", "asthma", "pcos", "heart-disease", "arthritis", "migraine" };

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public GetHealthWikiConditionsHandler(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<GetHealthWikiConditionsResponseModel> Handle(GetHealthWikiConditionsRequestModel request, CancellationToken cancellationToken)
        {
            var configured = (_configuration["HealthWiki:Conditions"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var used = await _context.HealthArticles.AsNoTracking()
                .Where(a => a.RelatedConditionSlug != null).Select(a => a.RelatedConditionSlug!).Distinct().ToListAsync(cancellationToken);

            var all = (configured.Length > 0 ? configured : Defaults).Concat(used)
                .Select(s => s.ToLowerInvariant()).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
            return new GetHealthWikiConditionsResponseModel { Conditions = all };
        }
    }

    /// <summary>Numbers on the CMS Health Wiki tabs: contributors waiting for verification and open topic requests.</summary>
    public class GetHealthWikiSummaryHandler : IRequestHandler<GetHealthWikiSummaryRequestModel, GetHealthWikiSummaryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHealthWikiSummaryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHealthWikiSummaryResponseModel> Handle(GetHealthWikiSummaryRequestModel request, CancellationToken cancellationToken)
        {
            return new GetHealthWikiSummaryResponseModel
            {
                PendingContributors = await _context.HealthWikiContributors.CountAsync(c => c.Status == HealthWikiContributor.StatusPending, cancellationToken),
                // "Open" = the team can still act on it, which matches TOPIC_OPEN in the CMS (SUBMITTED).
                OpenTopics = await _context.HealthWikiTopicRequests.CountAsync(t => t.Status == HealthWikiTopicRequest.StatusSubmitted, cancellationToken),
            };
        }
    }
}
