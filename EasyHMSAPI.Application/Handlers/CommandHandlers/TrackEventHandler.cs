using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Records one funnel/behavior event for the CMS Insights tab (Auth Funnel / Booking Funnel /
    // All Searches). Best-effort throughout, same reasoning as TrackVisitHandler — a visitor must
    // never see an error, or have the app misbehave, just because analytics recording had a bad
    // moment.
    public class TrackEventHandler : IRequestHandler<TrackEventRequestModel, TrackEventResponseModel>
    {
        private static readonly HashSet<string> ValidEventTypes = new()
        {
            AppConstants.AnalyticsEventType_LoginInitiated,
            AppConstants.AnalyticsEventType_OtpSent,
            AppConstants.AnalyticsEventType_OtpVerified,
            AppConstants.AnalyticsEventType_OtpVerifyFailed,
            AppConstants.AnalyticsEventType_SearchPerformed,
            AppConstants.AnalyticsEventType_DoctorProfileViewed,
            AppConstants.AnalyticsEventType_BookingStepReached,
        };

        private readonly AppDbContext _context;
        private readonly IGeoIpLookupService _geoIpLookupService;
        private readonly ILogger<TrackEventHandler> _logger;

        public TrackEventHandler(AppDbContext context, IGeoIpLookupService geoIpLookupService, ILogger<TrackEventHandler> logger)
        {
            _context = context;
            _geoIpLookupService = geoIpLookupService;
            _logger = logger;
        }

        public async Task<TrackEventResponseModel> Handle(TrackEventRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.EventType) || !ValidEventTypes.Contains(request.EventType))
                return new TrackEventResponseModel { Success = false };

            var geo = await _geoIpLookupService.LookupAsync(request.IpAddress, cancellationToken);

            var evt = new AnalyticsEvent
            {
                EventId = Guid.NewGuid(),
                EventType = request.EventType,
                OccurredAt = DateTime.UtcNow,
                SessionId = Truncate(request.SessionId, 64),
                Mobile = Truncate(request.Mobile, 20),
                DoctorId = request.DoctorId,
                SpecialtyId = Truncate(request.SpecialtyId, 100),
                IpAddress = request.IpAddress,
                Country = geo?.Country,
                Region = geo?.Region,
                City = geo?.City,
                MetadataJson = request.MetadataJson,
            };

            try
            {
                _context.AnalyticsEvents.Add(evt);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record analytics event {EventType}", request.EventType);
                return new TrackEventResponseModel { Success = false };
            }

            return new TrackEventResponseModel { Success = true };
        }

        private static string? Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) ? value : (value.Length <= maxLength ? value : value[..maxLength]);
    }
}
