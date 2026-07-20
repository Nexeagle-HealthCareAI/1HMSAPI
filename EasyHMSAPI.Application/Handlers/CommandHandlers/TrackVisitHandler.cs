using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Records one page-view beacon for the CMS "Site Visits" report. Region lookup is best-effort
    // (see IGeoIpLookupService) — a lookup failure still records the visit, just without
    // country/region/city filled in, since knowing SOMETHING was visited matters more than the
    // region enrichment succeeding every time.
    public class TrackVisitHandler : IRequestHandler<TrackVisitRequestModel, TrackVisitResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IGeoIpLookupService _geoIpLookupService;
        private readonly ILogger<TrackVisitHandler> _logger;

        public TrackVisitHandler(AppDbContext context, IGeoIpLookupService geoIpLookupService, ILogger<TrackVisitHandler> logger)
        {
            _context = context;
            _geoIpLookupService = geoIpLookupService;
            _logger = logger;
        }

        public async Task<TrackVisitResponseModel> Handle(TrackVisitRequestModel request, CancellationToken cancellationToken)
        {
            var geo = await _geoIpLookupService.LookupAsync(request.IpAddress, cancellationToken);

            var visit = new WebsiteVisit
            {
                VisitId = Guid.NewGuid(),
                VisitedAt = DateTime.UtcNow,
                IpAddress = request.IpAddress,
                Country = geo?.Country,
                Region = geo?.Region,
                City = geo?.City,
                PagePath = Truncate(request.PagePath, 500),
                ReferrerUrl = Truncate(request.ReferrerUrl, 500),
                UtmSource = Truncate(request.UtmSource, 100),
                UtmMedium = Truncate(request.UtmMedium, 100),
                UtmCampaign = Truncate(request.UtmCampaign, 100),
                UserAgent = Truncate(request.UserAgent, 500),
                SessionId = Truncate(request.SessionId, 64),
            };

            try
            {
                _context.WebsiteVisits.Add(visit);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Never let a visit-tracking write failure surface as an error to a site visitor.
                _logger.LogWarning(ex, "Failed to record website visit");
                return new TrackVisitResponseModel { Success = false };
            }

            return new TrackVisitResponseModel { Success = true };
        }

        private static string? Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) ? value : (value.Length <= maxLength ? value : value[..maxLength]);
    }
}
