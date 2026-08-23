using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
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
                var isCharge = string.Equals(type, BillingConstants.BillingActionType.Charges, StringComparison.OrdinalIgnoreCase);
                var isPayment = string.Equals(type, BillingConstants.BillingActionType.Payment, StringComparison.OrdinalIgnoreCase);
                if (!isCharge && !isPayment)
                {
                    return new DeleteBillingEventResponseModel
                    {
                        Success = false,
                        Message = "Invalid type. Must be 'Charges' or 'Payment'."
                    };
                }

                if (isCharge)
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
            // HospitalAccessFilter only proves the caller belongs to request.HospitalId -- it says
            // nothing about whether EventId itself belongs to that hospital. Without this filter,
            // any billing user could delete/void another hospital's charge by ID alone (e.g. a GUID
            // leaked in a screenshot, log line, or shared printed document). Everything looked up
            // below (invoiceChargeEvents, billingInvoice, allocations) is derived FROM this
            // already-scoped chargeEvent, so scoping this one lookup is sufficient.
            var chargeEvent = await _context.BillingChargeEvent
                .Where(bce => bce.ChargeEventId == request.EventId && bce.HospitalId == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);

            if (chargeEvent == null)
            {
                return new DeleteBillingEventResponseModel
                {
                    Success = false,
                    Message = "Charge event not found."
                };
            }

            // Same day-lock enforcement as UpdateChargeEventHandler -- see its comment. A closed
            // interim bill has already been printed/handed out; voiding one of its charges here
            // would silently make it disagree with the live ledger, with no invoice-FINALIZED
            // check catching it (day-closes happen mid-stay, well before discharge/finalize).
            var dayBillLineForDelete = await _context.AdmissionDayBillLine
                .Where(l => l.ChargeEventId == request.EventId && l.HospitalId == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);
            if (dayBillLineForDelete != null)
            {
                var dayBillForDelete = await _context.AdmissionDayBill
                    .Where(b => b.AdmissionDayBillId == dayBillLineForDelete.AdmissionDayBillId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (dayBillForDelete != null && dayBillForDelete.StatusCode == BillingConstants.DayBillStatus.Closed)
                {
                    return new DeleteBillingEventResponseModel
                    {
                        Success = false,
                        Message = $"Cannot delete this charge — it's part of Day {dayBillForDelete.DayNumber}'s closed interim bill ({dayBillForDelete.InterimBillNo}). Reopen that day first."
                    };
                }
            }

            var invoiceChargeEvents = await _context.BillingInvoiceChargeEvent
                .Where(bice => bice.ChargeEventId == request.EventId)
                .ToListAsync(cancellationToken);

            if (invoiceChargeEvents.Count == 0)
            {
                // Posted but never linked to a draft invoice (e.g. BillingPage's best-effort
                // auto-createDraftInvoice call failed silently) -- there's no invoice to unwind,
                // so just remove the charge directly instead of refusing to delete it forever.
                await ConsultantIncentiveHelper.CancelForChargeAsync(_context, chargeEvent.ChargeEventId, request.LoggedInUserName, "Charge event deleted", cancellationToken);
                _context.BillingChargeEvent.Remove(chargeEvent);
                await _context.SaveChangesAsync(cancellationToken);

                return new DeleteBillingEventResponseModel
                {
                    Success = true,
                    Message = "Charge event deleted successfully."
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

            // Reverse any payment money already applied to this specific charge (per-charge
            // breakdown built by PaymentAllocationHelper) — it becomes unallocated again and is
            // swept back onto the next invoice draft for this encounter, same as a fresh deposit.
            var allocationCharges = await _context.BillingPaymentAllocationCharge
                .Where(ac => ac.ChargeEventId == request.EventId)
                .ToListAsync(cancellationToken);
            if (allocationCharges.Count > 0)
            {
                var allocationIds = allocationCharges.Select(ac => ac.AllocationId).Distinct().ToList();
                var allocations = await _context.BillingPaymentAllocation
                    .Where(a => allocationIds.Contains(a.AllocationId))
                    .ToListAsync(cancellationToken);
                var allocationsById = allocations.ToDictionary(a => a.AllocationId);

                foreach (var ac in allocationCharges)
                {
                    if (allocationsById.TryGetValue(ac.AllocationId, out var allocation))
                        allocation.AllocatedAmount -= ac.Amount;
                }
                _context.BillingPaymentAllocationCharge.RemoveRange(allocationCharges);

                // An allocation reduced to (near) zero no longer represents a real invoice
                // contribution — drop it so it doesn't linger as a zero/negative-amount row.
                foreach (var allocation in allocations.Where(a => a.AllocatedAmount <= 0))
                    _context.BillingPaymentAllocation.Remove(allocation);
            }

            await ConsultantIncentiveHelper.CancelForChargeAsync(_context, chargeEvent.ChargeEventId, request.LoggedInUserName, "Charge event deleted", cancellationToken);
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
            // Same tenant-scoping fix as DeleteChargeEvent above -- see its comment.
            var payment = await _context.BillingPayment
                .Where(bp => bp.PaymentId == request.EventId && bp.HospitalId == request.HospitalId)
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

            if (paymentAllocations.Count > 0)
            {
                var allocationIds = paymentAllocations.Select(a => a.AllocationId).ToList();
                var allocationCharges = await _context.BillingPaymentAllocationCharge
                    .Where(ac => allocationIds.Contains(ac.AllocationId))
                    .ToListAsync(cancellationToken);
                if (allocationCharges.Count > 0)
                    _context.BillingPaymentAllocationCharge.RemoveRange(allocationCharges);
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
