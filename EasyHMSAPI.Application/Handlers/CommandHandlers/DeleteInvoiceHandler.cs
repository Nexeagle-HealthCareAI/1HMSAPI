using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeleteInvoiceHandler : IRequestHandler<DeleteInvoiceRequestModel, DeleteInvoiceResponseModel>
    {
        private readonly AppDbContext _context;

        public DeleteInvoiceHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DeleteInvoiceResponseModel> Handle(DeleteInvoiceRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty)
                    return new DeleteInvoiceResponseModel { Success = false, Message = "HospitalId and EncounterId are required." };

                if (string.IsNullOrWhiteSpace(request.Reason))
                    return new DeleteInvoiceResponseModel { Success = false, Message = "A reason is required to delete an invoice." };

                var invoice = await _context.BillingInvoice
                    .Where(bi => bi.EncounterId == request.EncounterId && bi.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (invoice == null)
                    return new DeleteInvoiceResponseModel { Success = false, Message = "Invoice not found." };

                if (invoice.StatusCode == BillingConstants.InvoiceStatus.Cancelled)
                    return new DeleteInvoiceResponseModel { Success = false, Message = "This invoice has already been deleted." };

                var reason = request.Reason.Trim();
                var now = DateTime.UtcNow;

                // Void every charge that was on this invoice -- as if none of it happened, not just
                // unlinked from the bill.
                var chargeEventIds = await _context.BillingInvoiceChargeEvent
                    .Where(bice => bice.InvoiceId == invoice.InvoiceId)
                    .Select(bice => bice.ChargeEventId)
                    .ToListAsync(cancellationToken);

                var chargeEvents = await _context.BillingChargeEvent
                    .Where(bce => chargeEventIds.Contains(bce.ChargeEventId) && bce.StatusCode != BillingConstants.ChargeEventStatus.Void)
                    .ToListAsync(cancellationToken);

                foreach (var chargeEvent in chargeEvents)
                {
                    chargeEvent.StatusCode = BillingConstants.ChargeEventStatus.Void;
                    chargeEvent.VoidedAt = now;
                    chargeEvent.VoidedBy = request.LoggedInUserName;
                    chargeEvent.VoidReason = $"Invoice deleted: {reason}";
                    chargeEvent.UpdatedAt = now;
                    chargeEvent.UpdatedBy = request.LoggedInUserName;

                    await ConsultantIncentiveHelper.CancelForChargeAsync(_context, chargeEvent.ChargeEventId, request.LoggedInUserName, $"Invoice deleted: {reason}", cancellationToken);
                }

                // Any money already collected and allocated to this invoice becomes unallocated
                // again -- the payment itself (the cash movement) is untouched, only its allocation
                // to this now-deleted invoice is reversed, same as one-charge deletion already does.
                var allocations = await _context.BillingPaymentAllocation
                    .Where(a => a.InvoiceId == invoice.InvoiceId)
                    .ToListAsync(cancellationToken);

                if (allocations.Count > 0)
                {
                    var allocationIds = allocations.Select(a => a.AllocationId).ToList();
                    var allocationCharges = await _context.BillingPaymentAllocationCharge
                        .Where(ac => allocationIds.Contains(ac.AllocationId))
                        .ToListAsync(cancellationToken);
                    if (allocationCharges.Count > 0)
                        _context.BillingPaymentAllocationCharge.RemoveRange(allocationCharges);

                    _context.BillingPaymentAllocation.RemoveRange(allocations);
                }

                invoice.StatusCode = BillingConstants.InvoiceStatus.Cancelled;
                invoice.CancelledAt = now;
                invoice.CancelledBy = request.LoggedInUserName;
                invoice.CancelReason = reason;
                invoice.GrossAmount = 0;
                invoice.DiscountAmount = 0;
                invoice.NetAmount = 0;
                invoice.TaxableAmount = 0;
                invoice.CgstAmount = 0;
                invoice.SgstAmount = 0;
                invoice.IgstAmount = 0;
                invoice.TaxAmount = 0;
                invoice.UpdatedAt = now;
                invoice.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new DeleteInvoiceResponseModel
                {
                    Success = true,
                    Message = "Invoice deleted.",
                    ChargesVoided = chargeEvents.Count,
                };
            }
            catch (Exception)
            {
                return new DeleteInvoiceResponseModel { Success = false, Message = "Error deleting invoice." };
            }
        }
    }
}
