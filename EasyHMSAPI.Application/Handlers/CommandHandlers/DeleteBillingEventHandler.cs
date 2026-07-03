using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeleteBillingEventHandler : IRequestHandler<DeleteBillingEventRequestModel, DeleteBillingEventResponseModel>
    {
        private readonly AppDbContext _context;

        public DeleteBillingEventHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DeleteBillingEventResponseModel> Handle(DeleteBillingEventRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var type = (request.Type ?? string.Empty).Trim();
                if (!string.Equals(type, BillingConstants.BillingActionType.Charges, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(type, BillingConstants.BillingActionType.Payment, StringComparison.OrdinalIgnoreCase))
                {
                    return new DeleteBillingEventResponseModel
                    {
                        Success = false,
                        Message = "Invalid type. Must be 'Charges' or 'Payment'."
                    };
                }

                if (string.Equals(type, BillingConstants.BillingActionType.Charges, StringComparison.OrdinalIgnoreCase))
                {
                    return await DeleteChargeEvent(request, cancellationToken);
                }
                else
                {
                    return await DeletePaymentEvent(request, cancellationToken);
                }
            }
            catch (Exception)
            {
                return new DeleteBillingEventResponseModel
                {
                    Success = false,
                    Message = "Error deleting billing event."
                };
            }
        }

        private async Task<DeleteBillingEventResponseModel> DeleteChargeEvent(DeleteBillingEventRequestModel request, CancellationToken cancellationToken)
        {
            var chargeEvent = await _context.BillingChargeEvent
                .Where(bce => bce.ChargeEventId == request.EventId)
                .FirstOrDefaultAsync(cancellationToken);

            if (chargeEvent == null)
            {
                return new DeleteBillingEventResponseModel
                {
                    Success = false,
                    Message = "Charge event not found."
                };
            }

            var invoiceChargeEvents = await _context.BillingInvoiceChargeEvent
                .Where(bice => bice.ChargeEventId == request.EventId)
                .ToListAsync(cancellationToken);

            if (invoiceChargeEvents.Count == 0)
            {
                return new DeleteBillingEventResponseModel
                {
                    Success = false,
                    Message = "No invoice mapping found for this charge event."
                };
            }

            var invoiceId = invoiceChargeEvents.First().InvoiceId;
            var billingInvoice = await _context.BillingInvoice
                .Where(bi => bi.InvoiceId == invoiceId)
                .FirstOrDefaultAsync(cancellationToken);

            if (billingInvoice == null)
            {
                return new DeleteBillingEventResponseModel
                {
                    Success = false,
                    Message = "Billing invoice not found."
                };
            }

            // Never mutate a locked invoice: deleting a charge from a finalized/cancelled bill
            // would silently rewrite immutable totals.
            if (string.Equals(billingInvoice.StatusCode, BillingConstants.InvoiceStatus.Finalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(billingInvoice.StatusCode, BillingConstants.InvoiceStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            {
                return new DeleteBillingEventResponseModel
                {
                    Success = false,
                    Message = "Cannot delete a charge from a finalized or cancelled invoice."
                };
            }

            _context.BillingChargeEvent.Remove(chargeEvent);

            foreach (var mapping in invoiceChargeEvents)
            {
                _context.BillingInvoiceChargeEvent.Remove(mapping);
            }

            // Recalculate invoice totals from the remaining linked charges.
            var remainingChargeEventIds = await _context.BillingInvoiceChargeEvent
                .Where(bice => bice.InvoiceId == invoiceId)
                .Select(bice => bice.ChargeEventId)
                .ToListAsync(cancellationToken);

            var remainingCharges = await _context.BillingChargeEvent
                .Where(bce => remainingChargeEventIds.Contains(bce.ChargeEventId))
                .ToListAsync(cancellationToken);

            decimal totalGrossAmount = 0;
            decimal totalDiscountAmount = 0;
            decimal totalNetAmount = 0;
            decimal totalTaxableAmount = 0;
            decimal totalCgst = 0;
            decimal totalSgst = 0;
            decimal totalIgst = 0;
            decimal totalTax = 0;

            foreach (var chargeDetail in remainingCharges)
            {
                // Fallback to Qty*UnitPrice for legacy rows persisted before GrossAmount was stored.
                var gross = chargeDetail.GrossAmount ?? (chargeDetail.Qty * chargeDetail.UnitPrice);
                var discount = chargeDetail.DiscountAmount ?? 0;
                totalGrossAmount += gross;
                totalDiscountAmount += discount;
                totalNetAmount += chargeDetail.NetAmount;
                totalTaxableAmount += chargeDetail.TaxableAmount ?? 0;
                totalCgst += chargeDetail.CgstAmount;
                totalSgst += chargeDetail.SgstAmount;
                totalIgst += chargeDetail.IgstAmount;
                totalTax += chargeDetail.TaxAmount;
            }

            billingInvoice.GrossAmount = totalGrossAmount;
            billingInvoice.DiscountAmount = totalDiscountAmount;
            billingInvoice.NetAmount = totalNetAmount;
            billingInvoice.TaxableAmount = totalTaxableAmount;
            billingInvoice.CgstAmount = totalCgst;
            billingInvoice.SgstAmount = totalSgst;
            billingInvoice.IgstAmount = totalIgst;
            billingInvoice.TaxAmount = totalTax;
            billingInvoice.UpdatedAt = DateTime.UtcNow;
            billingInvoice.UpdatedBy = request.LoggedInUserName;
            _context.BillingInvoice.Update(billingInvoice);

            await _context.SaveChangesAsync(cancellationToken);

            return new DeleteBillingEventResponseModel
            {
                Success = true,
                Message = "Charge event deleted successfully."
            };
        }

        private async Task<DeleteBillingEventResponseModel> DeletePaymentEvent(DeleteBillingEventRequestModel request, CancellationToken cancellationToken)
        {
            var payment = await _context.BillingPayment
                .Where(bp => bp.PaymentId == request.EventId)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
            {
                return new DeleteBillingEventResponseModel
                {
                    Success = false,
                    Message = "Payment event not found."
                };
            }

            var paymentAllocations = await _context.BillingPaymentAllocation
                .Where(bpa => bpa.PaymentId == request.EventId)
                .ToListAsync(cancellationToken);

            if (paymentAllocations.Count > 0)
            {
                var invoiceId = paymentAllocations.First().InvoiceId;
                var billingInvoice = await _context.BillingInvoice
                    .Where(bi => bi.InvoiceId == invoiceId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (billingInvoice != null)
                {
                    billingInvoice.UpdatedAt = DateTime.UtcNow;
                    billingInvoice.UpdatedBy = request.LoggedInUserName;
                    _context.BillingInvoice.Update(billingInvoice);
                }
            }

            foreach (var allocation in paymentAllocations)
            {
                _context.BillingPaymentAllocation.Remove(allocation);
            }

            _context.BillingPayment.Remove(payment);

            await _context.SaveChangesAsync(cancellationToken);

            return new DeleteBillingEventResponseModel
            {
                Success = true,
                Message = "Payment event deleted successfully."
            };
        }
    }
}
