using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class FinalizeBillingHandler : IRequestHandler<FinalizeBillingRequestModel, FinalizeBillingResponseModel>
    {
        private readonly AppDbContext _context;

        public FinalizeBillingHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<FinalizeBillingResponseModel> Handle(FinalizeBillingRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var action = (request.Type ?? string.Empty).Trim().ToLowerInvariant();
                if (action != BillingConstants.BillingActionType.Finalize && action != BillingConstants.BillingActionType.Reopen)
                {
                    return new FinalizeBillingResponseModel
                    {
                        Success = false,
                        Message = "Invalid type. Must be 'finalize' or 'reopen'."
                    };
                }

                var encounter = await _context.Encounter
                    .Where(e => e.EncounterId == request.EncounterId && e.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (encounter == null)
                {
                    return new FinalizeBillingResponseModel
                    {
                        Success = false,
                        Message = "Encounter not found."
                    };
                }

                var billingInvoice = await _context.BillingInvoice
                    .Where(bi => bi.EncounterId == request.EncounterId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (billingInvoice == null)
                {
                    return new FinalizeBillingResponseModel
                    {
                        Success = false,
                        Message = "Billing invoice not found."
                    };
                }

                if (action == BillingConstants.BillingActionType.Finalize)
                {
                    if (billingInvoice.StatusCode == BillingConstants.InvoiceStatus.Finalized)
                    {
                        return new FinalizeBillingResponseModel
                        {
                            Success = false,
                            Message = "Bill is already finalized."
                        };
                    }

                    // Block finalisation if any linked charge has a PENDING discount approval.
                    var pendingApprovals = await (
                        from a in _context.DiscountApproval
                        join bice in _context.BillingInvoiceChargeEvent on a.ChargeEventId equals bice.ChargeEventId
                        where bice.InvoiceId == billingInvoice.InvoiceId
                           && a.HospitalId == request.HospitalId
                           && a.Status == "PENDING"
                        select a.DiscountApprovalId
                    ).CountAsync(cancellationToken);

                    if (pendingApprovals > 0)
                    {
                        return new FinalizeBillingResponseModel
                        {
                            Success = false,
                            Message = $"Cannot finalize: {pendingApprovals} discount approval(s) pending."
                        };
                    }

                    encounter.StatusCode = BillingConstants.EncounterStatus.Finalized;
                    encounter.UpdatedAt = DateTime.UtcNow;
                    encounter.UpdatedBy = request.LoggedInUserName;
                    _context.Encounter.Update(encounter);

                    billingInvoice.StatusCode = BillingConstants.InvoiceStatus.Finalized;
                    billingInvoice.FinalizedAt = DateTime.UtcNow;
                    billingInvoice.FinalizedBy = request.LoggedInUserName;
                    billingInvoice.UpdatedAt = DateTime.UtcNow;
                    billingInvoice.UpdatedBy = request.LoggedInUserName;
                    _context.BillingInvoice.Update(billingInvoice);

                    var chargeEventIds = await _context.BillingInvoiceChargeEvent
                        .Where(bice => bice.InvoiceId == billingInvoice.InvoiceId)
                        .Select(bice => bice.ChargeEventId)
                        .ToListAsync(cancellationToken);

                    var chargeEvents = await _context.BillingChargeEvent
                        .Where(bce => chargeEventIds.Contains(bce.ChargeEventId))
                        .ToListAsync(cancellationToken);

                    foreach (var chargeEvent in chargeEvents)
                    {
                        chargeEvent.StatusCode = BillingConstants.ChargeEventStatus.Invoiced;
                        chargeEvent.UpdatedAt = DateTime.UtcNow;
                        chargeEvent.UpdatedBy = request.LoggedInUserName;
                        _context.BillingChargeEvent.Update(chargeEvent);
                    }

                    await _context.SaveChangesAsync(cancellationToken);

                    return new FinalizeBillingResponseModel
                    {
                        Success = true,
                        Message = "Bill finalized successfully."
                    };
                }
                else
                {
                    if (billingInvoice.StatusCode != BillingConstants.InvoiceStatus.Finalized)
                    {
                        return new FinalizeBillingResponseModel
                        {
                            Success = false,
                            Message = "Bill is not finalized, cannot reopen."
                        };
                    }

                    if (string.IsNullOrEmpty(request.Reason))
                    {
                        return new FinalizeBillingResponseModel
                        {
                            Success = false,
                            Message = "Reason is required to reopen the bill."
                        };
                    }

                    encounter.StatusCode = BillingConstants.EncounterStatus.Open;
                    encounter.UpdatedAt = DateTime.UtcNow;
                    encounter.UpdatedBy = request.LoggedInUserName;
                    _context.Encounter.Update(encounter);

                    billingInvoice.StatusCode = BillingConstants.InvoiceStatus.Draft;
                    billingInvoice.IsReopened = true;
                    billingInvoice.ReopenedReason = request.Reason;
                    billingInvoice.UpdatedAt = DateTime.UtcNow;
                    billingInvoice.UpdatedBy = request.LoggedInUserName;
                    _context.BillingInvoice.Update(billingInvoice);

                    var chargeEventIds = await _context.BillingInvoiceChargeEvent
                        .Where(bice => bice.InvoiceId == billingInvoice.InvoiceId)
                        .Select(bice => bice.ChargeEventId)
                        .ToListAsync(cancellationToken);

                    var chargeEvents = await _context.BillingChargeEvent
                        .Where(bce => chargeEventIds.Contains(bce.ChargeEventId))
                        .ToListAsync(cancellationToken);

                    foreach (var chargeEvent in chargeEvents)
                    {
                        chargeEvent.StatusCode = BillingConstants.ChargeEventStatus.Posted;
                        chargeEvent.UpdatedAt = DateTime.UtcNow;
                        chargeEvent.UpdatedBy = request.LoggedInUserName;
                        _context.BillingChargeEvent.Update(chargeEvent);
                    }

                    await _context.SaveChangesAsync(cancellationToken);

                    return new FinalizeBillingResponseModel
                    {
                        Success = true,
                        Message = "Bill reopened successfully."
                    };
                }
            }
            catch (Exception)
            {
                return new FinalizeBillingResponseModel
                {
                    Success = false,
                    Message = "Error finalizing billing."
                };
            }
        }
    }
}
