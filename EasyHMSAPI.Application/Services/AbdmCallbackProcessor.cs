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
        private readonly ILogger<AbdmCallbackProcessor> _logger;

        public AbdmCallbackProcessor(AppDbContext db, IAbdmLinkingService linking, ILogger<AbdmCallbackProcessor> logger)
        {
            _db = db;
            _linking = linking;
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
                else if (!path.EndsWith("links/context/on-notify", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith("patients/sms/on-notify", StringComparison.OrdinalIgnoreCase))
                    // Discovery, link init/confirm, consent and data requests are built in later
                    // phases. Left unprocessed on purpose: "ABDM asked and we have not replied" is a
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
