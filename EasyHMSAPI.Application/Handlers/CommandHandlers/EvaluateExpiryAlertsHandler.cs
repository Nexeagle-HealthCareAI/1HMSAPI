using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Batch-expiry counterpart to EvaluateAlertsHandler: scans ACTIVE batches with remaining stock
    // and raises an in-app Alert (+ one digest SMS to the hospital's main contact) the first time a
    // batch crosses the 90/60/30-day thresholds. Idempotent per (batchId, code), same dedup pattern
    // as EvaluateAlertsHandler's (admission, code) keys — so as a batch ages toward expiry it can
    // accumulate up to three alerts (90, then 60, then 30) but never a duplicate of the same one.
    public class EvaluateExpiryAlertsHandler : IRequestHandler<EvaluateExpiryAlertsRequestModel, EvaluateExpiryAlertsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly ILogger<EvaluateExpiryAlertsHandler> _logger;

        private const string Expiry90Code = "EXPIRY_90";
        private const string Expiry60Code = "EXPIRY_60";
        private const string Expiry30Code = "EXPIRY_30";

        public EvaluateExpiryAlertsHandler(AppDbContext context, ISmsService smsService, ILogger<EvaluateExpiryAlertsHandler> logger)
        {
            _context = context;
            _smsService = smsService;
            _logger = logger;
        }

        public async Task<EvaluateExpiryAlertsResponseModel> Handle(EvaluateExpiryAlertsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var horizon = today.AddDays(90);

                var batches = await _context.Batch.AsNoTracking()
                    .Where(b => b.HospitalId == request.HospitalId && b.Status == "ACTIVE" && b.RemainingQty > 0
                             && b.ExpiryDate != null && b.ExpiryDate.Value.Date <= horizon)
                    .ToListAsync(cancellationToken);

                var response = new EvaluateExpiryAlertsResponseModel { Success = true, BatchesScanned = batches.Count };
                if (batches.Count == 0)
                {
                    response.Message = "No near-expiry batches to evaluate.";
                    return response;
                }

                var batchIds = batches.Select(b => b.BatchId).ToList();
                var existingKeys = (await _context.Alert.AsNoTracking()
                        .Where(a => a.HospitalId == request.HospitalId && a.Status == "ACTIVE"
                                 && a.SourceModule == "EXPIRY_ALERT_EVALUATOR" && a.SourceRefId != null)
                        .Select(a => new { a.SourceRefId, a.AlertCode })
                        .ToListAsync(cancellationToken))
                    .Select(x => $"{x.SourceRefId}|{x.AlertCode}")
                    .ToHashSet();

                var itemNames = await _context.InventoryItem
                    .Where(i => batches.Select(b => b.InventoryItemId).Distinct().Contains(i.InventoryItemId))
                    .ToDictionaryAsync(i => i.InventoryItemId, i => i.ItemName, cancellationToken);

                var raisedByCode = new Dictionary<string, int> { [Expiry90Code] = 0, [Expiry60Code] = 0, [Expiry30Code] = 0 };

                foreach (var batch in batches)
                {
                    var daysToExpiry = (int)(batch.ExpiryDate!.Value.Date - today).TotalDays;
                    var code = daysToExpiry <= 30 ? Expiry30Code : daysToExpiry <= 60 ? Expiry60Code : Expiry90Code;

                    var key = $"{batch.BatchId}|{code}";
                    if (!existingKeys.Add(key))
                    {
                        response.AlertsSkippedDuplicate++;
                        continue;
                    }

                    var itemName = itemNames.TryGetValue(batch.InventoryItemId, out var name) ? name : "Unknown item";
                    _context.Alert.Add(new Alert
                    {
                        AlertId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        AlertCode = code,
                        Severity = code == Expiry30Code ? "CRITICAL" : "WARNING",
                        Title = $"{itemName} batch {batch.BatchNumber} expiring in {daysToExpiry} day(s)",
                        Body = $"Batch {batch.BatchNumber} of {itemName} ({batch.RemainingQty} remaining) expires on {batch.ExpiryDate:dd MMM yyyy}.",
                        Status = "ACTIVE",
                        RaisedAt = DateTime.UtcNow,
                        RaisedBy = request.LoggedInUserName,
                        RaisedByUserId = request.LoggedInUserId,
                        SourceModule = "EXPIRY_ALERT_EVALUATOR",
                        SourceRefId = batch.BatchId.ToString(),
                        DispatchSms = false,
                        DispatchWhatsApp = false,
                        DispatchInApp = true,
                        CreatedAt = DateTime.UtcNow,
                    });

                    raisedByCode[code]++;
                }

                response.AlertsRaised = raisedByCode.Values.Sum();
                if (response.AlertsRaised > 0)
                    await _context.SaveChangesAsync(cancellationToken);

                if (response.AlertsRaised > 0)
                {
                    var hospital = await _context.Hospitals.AsNoTracking()
                        .Where(h => h.HospitalID == request.HospitalId)
                        .Select(h => new { h.Contact, h.Name })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (hospital != null && !string.IsNullOrWhiteSpace(hospital.Contact))
                    {
                        var body = $"{hospital.Name} pharmacy: {raisedByCode[Expiry30Code]} batch(es) expiring within 30 days, "
                                 + $"{raisedByCode[Expiry60Code]} within 60, {raisedByCode[Expiry90Code]} within 90. Check the near-expiry report.";
                        try
                        {
                            var sent = await _smsService.SendInvitationSmsAsync(hospital.Contact, body);
                            if (sent) response.SmsDispatched = 1;
                        }
                        catch (Exception smsEx)
                        {
                            _logger.LogError(smsEx, "Failed to send expiry digest SMS for hospitalId: {HospitalId}", request.HospitalId);
                        }
                    }
                }

                response.Message = $"Scanned {response.BatchesScanned} batches; raised {response.AlertsRaised} alerts.";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating expiry alerts for hospitalId: {HospitalId}", request.HospitalId);
                return new EvaluateExpiryAlertsResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
