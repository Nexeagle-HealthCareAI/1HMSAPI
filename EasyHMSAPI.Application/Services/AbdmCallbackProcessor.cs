using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services
{
    public interface IAbdmCallbackProcessor
    {
        /// <summary>Acts on an accepted delivery, after its 202 has already gone back to ABDM.</summary>
        Task ProcessAsync(Guid inboundId, CancellationToken cancellationToken);
    }

    public sealed class AbdmCallbackProcessor : IAbdmCallbackProcessor
    {
        private readonly AppDbContext _db;
        private readonly IAbdmLinkingService _linking;
        private readonly IAbdmUserLinkingService _userLinking;
        private readonly IAbdmDataFlowService _dataFlow;
        private readonly IAbdmHiuService _hiu;
        private readonly ILogger<AbdmCallbackProcessor> _logger;

        public AbdmCallbackProcessor(AppDbContext db, IAbdmLinkingService linking, IAbdmUserLinkingService userLinking, IAbdmDataFlowService dataFlow, IAbdmHiuService hiu, ILogger<AbdmCallbackProcessor> logger)
        {
            _db = db;
            _linking = linking;
            _userLinking = userLinking;
            _dataFlow = dataFlow;
            _hiu = hiu;
            _logger = logger;
        }

        public async Task ProcessAsync(Guid inboundId, CancellationToken cancellationToken)
        {
            var inbound = await _db.AbdmInbounds.FirstOrDefaultAsync(i => i.InboundId == inboundId, cancellationToken);
            if (inbound == null || inbound.Verdict != AbdmInboundVerdict.Accepted)
                return;

            var path = inbound.Path.TrimEnd('/');
            try
            {
                if (path.EndsWith("token/on-generate-token", StringComparison.OrdinalIgnoreCase))
                    await _linking.OnLinkTokenAnsweredAsync(inbound, cancellationToken);
                else if (path.EndsWith("link/on_carecontext", StringComparison.OrdinalIgnoreCase))
                    await _linking.OnLinkAnsweredAsync(inbound, cancellationToken);
                else if (path.EndsWith("patient/care-context/discover", StringComparison.OrdinalIgnoreCase))
                    await _userLinking.OnDiscoverAsync(inbound, cancellationToken);
                else if (path.EndsWith("link/care-context/init", StringComparison.OrdinalIgnoreCase))
                    await _userLinking.OnInitAsync(inbound, cancellationToken);
                else if (path.EndsWith("link/care-context/confirm", StringComparison.OrdinalIgnoreCase))
                    await _userLinking.OnConfirmAsync(inbound, cancellationToken);
                else if (path.EndsWith("consent/request/hip/notify", StringComparison.OrdinalIgnoreCase))
                    await _dataFlow.OnConsentNotifyAsync(inbound, cancellationToken);
                else if (path.EndsWith("hip/health-information/request", StringComparison.OrdinalIgnoreCase))
                    await _dataFlow.OnHealthInformationRequestAsync(inbound, cancellationToken);
                else if (path.EndsWith("hiu/consent/request/on-init", StringComparison.OrdinalIgnoreCase))
                    await _hiu.OnConsentInitAnsweredAsync(inbound, cancellationToken);
                else if (path.EndsWith("hiu/consent/request/notify", StringComparison.OrdinalIgnoreCase))
                    await _hiu.OnConsentNotifyAsync(inbound, cancellationToken);
                else if (path.EndsWith("hiu/consent/request/on-status", StringComparison.OrdinalIgnoreCase))
                    await _hiu.OnConsentStatusAnsweredAsync(inbound, cancellationToken);
                else if (path.EndsWith("hiu/consent/on-fetch", StringComparison.OrdinalIgnoreCase))
                    await _hiu.OnConsentFetchedAsync(inbound, cancellationToken);
                else if (path.EndsWith("hiu/health-information/on-request", StringComparison.OrdinalIgnoreCase))
                    await _hiu.OnDataRequestAnsweredAsync(inbound, cancellationToken);
                else if (!path.EndsWith("links/context/on-notify", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith("patients/sms/on-notify", StringComparison.OrdinalIgnoreCase))
                    // Anything else is left unprocessed on purpose: "ABDM asked and we have not replied" is a
                    // state the panel shows, not silence.
                    return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ABDM callback {Path} ({InboundId}) failed while processing.", inbound.Path, inbound.InboundId);
                inbound.ProcessError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            }

            inbound.ProcessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
