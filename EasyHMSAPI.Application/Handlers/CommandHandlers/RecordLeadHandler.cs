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
    // Records one hospital-scoped marketing lead for the Lead Generation page. Best-effort
    // throughout -- same reasoning as TrackEventHandler -- neither a website visitor nor the
    // WhatsApp bot's own conversation flow should ever see an error, or have anything misbehave,
    // just because lead recording had a bad moment.
    public class RecordLeadHandler : IRequestHandler<RecordLeadRequestModel, RecordLeadResponseModel>
    {
        private static readonly HashSet<string> ValidSources = new()
        {
            AppConstants.LeadSource_DoctorDekho,
            AppConstants.LeadSource_WhatsApp,
        };

        private static readonly HashSet<string> ValidLeadTypes = new()
        {
            AppConstants.LeadType_DoctorNameSearch,
            AppConstants.LeadType_HospitalNameSearch,
            AppConstants.LeadType_DoctorProfileView,
            AppConstants.LeadType_HospitalPageView,
        };

        private readonly AppDbContext _context;
        private readonly IGeoIpLookupService _geoIpLookupService;
        private readonly ILogger<RecordLeadHandler> _logger;

        public RecordLeadHandler(AppDbContext context, IGeoIpLookupService geoIpLookupService, ILogger<RecordLeadHandler> logger)
        {
            _context = context;
            _geoIpLookupService = geoIpLookupService;
            _logger = logger;
        }

        public async Task<RecordLeadResponseModel> Handle(RecordLeadRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new RecordLeadResponseModel { Success = false };
            if (string.IsNullOrWhiteSpace(request.Source) || !ValidSources.Contains(request.Source))
                return new RecordLeadResponseModel { Success = false };
            if (string.IsNullOrWhiteSpace(request.LeadType) || !ValidLeadTypes.Contains(request.LeadType))
                return new RecordLeadResponseModel { Success = false };

            // Only resolved for web-sourced leads -- WhatsApp calls never set IpAddress (there's no
            // visitor IP to forward from a bot's own outbound HTTP call).
            var geo = request.IpAddress != null
                ? await _geoIpLookupService.LookupAsync(request.IpAddress, cancellationToken)
                : null;

            var lead = new HospitalLead
            {
                LeadId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                DoctorId = request.DoctorId,
                Source = request.Source,
                LeadType = request.LeadType,
                SearchQuery = Truncate(request.SearchQuery, 500),
                Mobile = Truncate(request.Mobile, 20),
                PatientName = Truncate(request.PatientName, 200),
                SessionId = Truncate(request.SessionId, 64),
                IpAddress = request.IpAddress,
                Country = geo?.Country,
                Region = geo?.Region,
                City = geo?.City,
                OccurredAt = DateTime.UtcNow,
            };

            try
            {
                _context.HospitalLeads.Add(lead);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record hospital lead {LeadType} for hospital {HospitalId}", request.LeadType, request.HospitalId);
                return new RecordLeadResponseModel { Success = false };
            }

            return new RecordLeadResponseModel { Success = true };
        }

        private static string? Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) ? value : (value.Length <= maxLength ? value : value[..maxLength]);
    }
}
