using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Corrects an already-posted charge line (qty/rate/discount/name) in place, instead of
    /// deleting and re-adding. Recomputes the line's monetary/tax figures using its EXISTING GST
    /// classification (rate/inclusive/inter-state) — editing corrects qty/rate/discount, not the
    /// tax treatment, which would need a bigger change than a "fix a typo" action. No discount-cap
    /// / admin-approval gate: every billing money-safety approval gate was removed per product
    /// decision (mirrors AddChargeEventHandler/AddPaymentEventHandler/DeleteBillingEventHandler/
    /// CreateDraftInvoiceHandler/FinalizeBillingHandler, all edited the same way).
    /// </summary>
    public class UpdateChargeEventHandler : IRequestHandler<UpdateChargeEventRequestModel, UpdateChargeEventResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdateChargeEventHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateChargeEventResponseModel> Handle(UpdateChargeEventRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.Qty <= 0)
                    return new UpdateChargeEventResponseModel { Success = false, Message = "Quantity must be greater than zero." };
                if (request.Rate < 0)
                    return new UpdateChargeEventResponseModel { Success = false, Message = "Rate cannot be negative." };
                if (request.DiscountPercent < 0)
                    return new UpdateChargeEventResponseModel { Success = false, Message = "Discount cannot be negative." };

                var chargeEvent = await _context.BillingChargeEvent
                    .Where(ce => ce.ChargeEventId == request.ChargeEventId && ce.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (chargeEvent == null)
                    return new UpdateChargeEventResponseModel { Success = false, Message = "Charge event not found." };

                if (chargeEvent.StatusCode == BillingConstants.ChargeEventStatus.Void)
                    return new UpdateChargeEventResponseModel { Success = false, Message = "Cannot edit a voided charge." };

                var invoiceLink = await _context.BillingInvoiceChargeEvent
                    .Where(bice => bice.ChargeEventId == request.ChargeEventId)
                    .FirstOrDefaultAsync(cancellationToken);

                var billingInvoice = invoiceLink == null
                    ? null
                    : await _context.BillingInvoice.Where(bi => bi.InvoiceId == invoiceLink.InvoiceId).FirstOrDefaultAsync(cancellationToken);

                if (billingInvoice != null
                    && (string.Equals(billingInvoice.StatusCode, BillingConstants.InvoiceStatus.Finalized, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(billingInvoice.StatusCode, BillingConstants.InvoiceStatus.Cancelled, StringComparison.OrdinalIgnoreCase)))
                {
                    return new UpdateChargeEventResponseModel { Success = false, Message = "Cannot edit a charge on a finalized or cancelled invoice." };
                }

                // A closed admission day's interim bill has already been printed/handed to the
                // patient or TPA -- the invoice's own FINALIZED status doesn't cover this case,
                // since day-wise closes happen mid-stay, well before discharge/finalize. Without
                // this check, editing a charge here would silently make the printed interim bill
                // and the live ledger disagree, with nothing catching it. ReopenAdmissionDayHandler
                // is the only path that removes a charge's AdmissionDayBillLine row, so its
                // presence (against a still-Closed day) is exactly "this charge is day-locked."
                var dayBillLine = await _context.AdmissionDayBillLine
                    .Where(l => l.ChargeEventId == request.ChargeEventId && l.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (dayBillLine != null)
                {
                    var dayBill = await _context.AdmissionDayBill
                        .Where(b => b.AdmissionDayBillId == dayBillLine.AdmissionDayBillId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (dayBill != null && dayBill.StatusCode == BillingConstants.DayBillStatus.Closed)
                    {
                        return new UpdateChargeEventResponseModel
                        {
                            Success = false,
                            Message = $"Cannot edit this charge — it's part of Day {dayBill.DayNumber}'s closed interim bill ({dayBill.InterimBillNo}). Reopen that day first."
                        };
                    }
                }

                // Isolate the invoice-level (overall, not tied to any one line) discount BEFORE this
                // charge's own DiscountAmount is mutated below — otherwise recomputing
                // billingInvoice.DiscountAmount from just the per-line sum would silently wipe out
                // an already-applied "Add Discount" amount every time any charge on the invoice is
                // edited. Mirrors CreateDraftInvoiceHandler's existingInvoiceLevelDiscount derivation.
                decimal invoiceLevelDiscount = 0;
                if (billingInvoice != null)
                {
                    var linkedChargeEventIdsForDiscount = await _context.BillingInvoiceChargeEvent
                        .Where(bice => bice.InvoiceId == billingInvoice.InvoiceId)
                        .Select(bice => bice.ChargeEventId)
                        .ToListAsync(cancellationToken);
                    var priorLineDiscountTotal = await _context.BillingChargeEvent
                        .Where(bce => linkedChargeEventIdsForDiscount.Contains(bce.ChargeEventId))
                        .SumAsync(bce => (decimal?)(bce.DiscountAmount ?? 0), cancellationToken) ?? 0m;
                    invoiceLevelDiscount = Math.Max(0, (billingInvoice.DiscountAmount ?? 0) - priorLineDiscountTotal);
                }

                // Never let an edit reduce a charge below the money already specifically allocated
                // to it — that would silently create an unfunded line. Increasing it, or editing
                // when nothing's been paid against it yet, is always safe.
                var alreadyPaidToCharge = await _context.BillingPaymentAllocationCharge
                    .Where(ac => ac.ChargeEventId == request.ChargeEventId)
                    .SumAsync(ac => (decimal?)ac.Amount, cancellationToken) ?? 0m;

                var gross = request.Qty * request.Rate;
                var discount = Math.Round(gross * (request.DiscountPercent / 100m), 2);
                if (discount > gross) discount = gross;
                var net = gross - discount;

                if (net < alreadyPaidToCharge)
                {
                    return new UpdateChargeEventResponseModel
                    {
                        Success = false,
                        Message = $"Cannot reduce this charge below the ₹{alreadyPaidToCharge:0.00} already paid against it."
                    };
                }

                var policy = await _context.BillingPolicy.FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
                var rounding = string.IsNullOrWhiteSpace(policy?.TaxRoundingMode) ? "ROUND" : policy!.TaxRoundingMode;

                var gstRate = chargeEvent.GstRate;
                var taxable = gstRate.HasValue && gstRate.Value > 0m
                    ? GstTaxComputer.Compute(net, gstRate, chargeEvent.IsTaxInclusive, chargeEvent.IsInterState, rounding)
                    : new GstTaxComputer.GstLineResult(net, 0, 0, 0, 0, net);

                chargeEvent.Qty = request.Qty;
                chargeEvent.UnitPrice = request.Rate;
                chargeEvent.GrossAmount = gross;
                chargeEvent.DiscountAmount = discount;
                chargeEvent.NetAmount = net;
                chargeEvent.TaxableAmount = taxable.TaxableAmount;
                chargeEvent.CgstAmount = taxable.CgstAmount;
                chargeEvent.SgstAmount = taxable.SgstAmount;
                chargeEvent.IgstAmount = taxable.IgstAmount;
                chargeEvent.TaxAmount = taxable.TaxAmount;
                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                    chargeEvent.DisplayName = request.DisplayName.Trim();
                chargeEvent.UpdatedAt = DateTime.UtcNow;
                chargeEvent.UpdatedBy = request.LoggedInUserName;

                var responseData = new UpdateChargeEventData
                {
                    Charge = new ChargeEventDetail
                    {
                        ChargeEventId = chargeEvent.ChargeEventId,
                        DisplayName = chargeEvent.DisplayName,
                        Qty = chargeEvent.Qty,
                        UnitPrice = chargeEvent.UnitPrice,
                        GrossAmount = chargeEvent.GrossAmount ?? gross,
                        DiscountAmount = chargeEvent.DiscountAmount ?? 0,
                        NetAmount = chargeEvent.NetAmount,
                        IncentiveAmount = chargeEvent.IncentiveAmount,
                        HsnSacCode = chargeEvent.HsnSacCode,
                        GstRate = chargeEvent.GstRate,
                        TaxableAmount = chargeEvent.TaxableAmount ?? 0,
                        CgstAmount = chargeEvent.CgstAmount,
                        SgstAmount = chargeEvent.SgstAmount,
                        IgstAmount = chargeEvent.IgstAmount,
                        TaxAmount = chargeEvent.TaxAmount,
                        IsTaxInclusive = chargeEvent.IsTaxInclusive,
                        IsInterState = chargeEvent.IsInterState,
                        DiscountApprovalId = null,
                        DiscountApprovalRequired = false,
                        DiscountCapPercent = null,
                    },
                };

                if (billingInvoice != null)
                {
                    var remainingChargeEventIds = await _context.BillingInvoiceChargeEvent
                        .Where(bice => bice.InvoiceId == billingInvoice.InvoiceId)
                        .Select(bice => bice.ChargeEventId)
                        .ToListAsync(cancellationToken);

                    // Identity-resolution means this re-query returns the SAME tracked chargeEvent
                    // instance above (with its in-memory, not-yet-saved changes) rather than a stale
                    // copy from the database — same pattern DeleteBillingEventHandler already relies on.
                    var remainingCharges = await _context.BillingChargeEvent
                        .Where(bce => remainingChargeEventIds.Contains(bce.ChargeEventId))
                        .ToListAsync(cancellationToken);

                    decimal totalGrossAmount = 0, totalLineDiscountAmount = 0;
                    decimal totalTaxableAmount = 0, totalCgst = 0, totalSgst = 0, totalIgst = 0, totalTax = 0;
                    foreach (var c in remainingCharges)
                    {
                        totalGrossAmount += c.GrossAmount ?? (c.Qty * c.UnitPrice);
                        totalLineDiscountAmount += c.DiscountAmount ?? 0;
                        totalTaxableAmount += c.TaxableAmount ?? 0;
                        totalCgst += c.CgstAmount;
                        totalSgst += c.SgstAmount;
                        totalIgst += c.IgstAmount;
                        totalTax += c.TaxAmount;
                    }

                    // Re-add the preserved invoice-level discount on top of the per-line total —
                    // same combined-total shape CreateDraftInvoiceHandler uses — and re-derive
                    // NetAmount from it, capping at gross so a shrunk invoice can't go negative.
                    decimal totalDiscountAmount = totalLineDiscountAmount + invoiceLevelDiscount;
                    if (totalDiscountAmount > totalGrossAmount) totalDiscountAmount = totalGrossAmount;
                    decimal totalNetAmount = totalGrossAmount - totalDiscountAmount;

                    // Same invoice-level-discount GST scaling as CreateDraftInvoiceHandler -- see
                    // its comment. totalTaxableAmount/totalCgst/totalSgst/totalIgst/totalTax above
                    // are pure per-line snapshot sums and never account for invoiceLevelDiscount,
                    // which isn't tied to any one line.
                    decimal netBeforeInvoiceLevelDiscount = totalGrossAmount - totalLineDiscountAmount;
                    if (invoiceLevelDiscount > 0 && netBeforeInvoiceLevelDiscount > 0)
                    {
                        var ratio = totalNetAmount / netBeforeInvoiceLevelDiscount;
                        totalTaxableAmount = Math.Round(totalTaxableAmount * ratio, 2);
                        totalCgst = Math.Round(totalCgst * ratio, 2);
                        totalSgst = Math.Round(totalSgst * ratio, 2);
                        totalIgst = Math.Round(totalIgst * ratio, 2);
                        totalTax = Math.Round(totalTax * ratio, 2);
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

                    responseData.InvoiceId = billingInvoice.InvoiceId;
                    responseData.InvoiceGrossAmount = totalGrossAmount;
                    responseData.InvoiceDiscountAmount = totalDiscountAmount;
                    responseData.InvoiceNetAmount = totalNetAmount;
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new UpdateChargeEventResponseModel
                {
                    Success = true,
                    Message = "Charge updated successfully.",
                    Data = responseData,
                };
            }
            catch (Exception)
            {
                return new UpdateChargeEventResponseModel { Success = false, Message = "Error updating charge." };
            }
        }
    }
}
