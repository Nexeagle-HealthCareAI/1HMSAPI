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
    public class AddPaymentEventHandler : IRequestHandler<AddPaymentEventRequestModel, AddPaymentEventResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public AddPaymentEventHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<AddPaymentEventResponseModel> Handle(AddPaymentEventRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.Payment == null)
                {
                    return new AddPaymentEventResponseModel
                    {
                        Success = false,
                        Message = "Payment details are required."
                    };
                }

                var normalizedPaymentType = (request.Payment.PaymentType ?? string.Empty).Trim().ToUpperInvariant();
                var allowedPaymentTypes = new[]
                {
                    BillingConstants.PaymentType.Payment,
                    BillingConstants.PaymentType.Advance,
                    BillingConstants.PaymentType.Refund
                };
                if (!allowedPaymentTypes.Contains(normalizedPaymentType))
                {
                    return new AddPaymentEventResponseModel
                    {
                        Success = false,
                        Message = "Invalid payment type. Must be 'PAYMENT', 'ADVANCE', or 'REFUND'."
                    };
                }
                request.Payment.PaymentType = normalizedPaymentType;

                // Guard against negative/zero amounts: a negative payment would slip past the
                // "exceeds remaining due" check below and re-open a paid invoice / inflate credit.
                if (request.Payment.Amount <= 0)
                {
                    return new AddPaymentEventResponseModel
                    {
                        Success = false,
                        Message = "Payment amount must be greater than zero."
                    };
                }

                // Prefer the DRAFT invoice (still mutable); fall back to the latest invoice.
                async Task<BillingInvoice?> LoadInvoiceAsync() => await _context.BillingInvoice
                    .Where(bi => bi.EncounterId == request.EncounterId)
                    .OrderByDescending(bi => bi.StatusCode == BillingConstants.InvoiceStatus.Draft)
                    .ThenByDescending(bi => bi.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                var billingInvoice = await LoadInvoiceAsync();

                // Recording a payment implies the encounter's posted charges are being billed.
                // Run the draft-invoice builder when there's no invoice yet OR when the current
                // invoice is still a DRAFT: CreateDraftInvoice reuses the draft and links any
                // newly posted charges (e.g. a lab added after the consult was already paid),
                // recomputing the total so the payment validates against the up-to-date due.
                // A FINALIZED invoice is left untouched (locked).
                if (billingInvoice == null || billingInvoice.StatusCode == BillingConstants.InvoiceStatus.Draft)
                {
                    await _mediator.Send(new CreateDraftInvoiceRequestModel
                    {
                        PatientId = request.PatientId,
                        EncounterId = request.EncounterId,
                        HospitalId = request.HospitalId,
                        LoggedInUserName = request.LoggedInUserName,
                    }, cancellationToken);

                    billingInvoice = await LoadInvoiceAsync();
                }

                if (billingInvoice == null)
                {
                    // No charges have been posted for this encounter yet — a plain PAYMENT/REFUND
                    // has nothing to apply against, but an ADVANCE is a valid deposit-before-any-
                    // charge scenario (e.g. collected at registration). Hold it charge-less;
                    // CreateDraftInvoiceHandler auto-allocates it once the first charge posts.
                    if (normalizedPaymentType == BillingConstants.PaymentType.Advance)
                    {
                        return await RecordChargeLessAdvanceAsync(request, cancellationToken);
                    }
                    return new AddPaymentEventResponseModel
                    {
                        Success = false,
                        Message = "No invoice could be created — there are no posted charges on this encounter to bill."
                    };
                }

                decimal netAmount = billingInvoice.NetAmount ?? 0;
                decimal allocatedAmount = 0;
                decimal creditAmount = 0;

                if (normalizedPaymentType == BillingConstants.PaymentType.Payment)
                {
                    // Backstop against double/over payment: measure against the REMAINING due
                    // (net − already-allocated), not the full invoice total, so a payment can never
                    // exceed what's owed regardless of which client sends it.
                    var totalPastPayments = await _context.BillingPaymentAllocation
                        .Where(bpa => bpa.InvoiceId == billingInvoice.InvoiceId)
                        .SumAsync(bpa => bpa.AllocatedAmount, cancellationToken);

                    decimal remainingDue = netAmount - totalPastPayments;

                    if (remainingDue <= 0)
                    {
                        return new AddPaymentEventResponseModel
                        {
                            Success = false,
                            Message = "This invoice is already fully paid."
                        };
                    }

                    if (request.Payment.Amount > remainingDue)
                    {
                        return new AddPaymentEventResponseModel
                        {
                            Success = false,
                            Message = $"Payment amount ({request.Payment.Amount}) cannot exceed the remaining due ({remainingDue})."
                        };
                    }

                    allocatedAmount = request.Payment.Amount;
                }
                else if (normalizedPaymentType == BillingConstants.PaymentType.Advance)
                {
                    var totalPastPayments = await _context.BillingPaymentAllocation
                        .Where(bpa => bpa.InvoiceId == billingInvoice.InvoiceId)
                        .SumAsync(bpa => bpa.AllocatedAmount, cancellationToken);

                    decimal remainingDue = netAmount - totalPastPayments;

                    // Excess over what's due is held directly as unallocated credit on the
                    // encounter — no admin sign-off required (approval gating removed).
                    if (request.Payment.Amount > remainingDue)
                    {
                        allocatedAmount = Math.Max(0, remainingDue);
                        creditAmount = request.Payment.Amount - allocatedAmount;
                    }
                    else
                    {
                        allocatedAmount = request.Payment.Amount;
                    }
                }
                else if (normalizedPaymentType == BillingConstants.PaymentType.Refund)
                {
                    // Whole-encounter credit, not just this one invoice's allocations: charges can
                    // fragment across multiple BillingInvoice rows (e.g. day-wise IPD finalize
                    // cycles) and money can sit unallocated (a charge-less advance, or money freed
                    // up by cancelling a paid charge) — none of that shows up in this invoice's own
                    // AllocatedAmount total. "Available credit" has to net total collected against
                    // total billed for the ENTIRE encounter, or a real credit balance reads as zero.
                    var totalCollected = await _context.BillingPayment
                        .Where(p => p.EncounterId == request.EncounterId
                                 && (p.PaymentType == BillingConstants.PaymentType.Payment || p.PaymentType == BillingConstants.PaymentType.Advance))
                        .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
                    var totalRefunded = await _context.BillingPayment
                        .Where(p => p.EncounterId == request.EncounterId && p.PaymentType == BillingConstants.PaymentType.Refund)
                        .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
                    var totalBilled = await _context.BillingChargeEvent
                        .Where(c => c.EncounterId == request.EncounterId && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                        .SumAsync(c => (decimal?)c.NetAmount, cancellationToken) ?? 0m;

                    decimal remainingDue = totalBilled - (totalCollected - totalRefunded);
                    decimal availableCredit = Math.Max(0, -remainingDue);

                    if (availableCredit <= 0 || request.Payment.Amount > availableCredit)
                    {
                        return new AddPaymentEventResponseModel
                        {
                            Success = false,
                            Message = $"Insufficient credit available. Available credit: {availableCredit}"
                        };
                    }

                    // A partial refund that still leaves the patient in credit is allowed directly
                    // now — no admin sign-off required (approval gating removed).
                    //
                    // Deliberately NOT setting allocatedAmount here: a refund is money paid back OUT
                    // to the patient, not money applied toward a charge. Setting it would fall into
                    // the `if (allocatedAmount > 0)` block below and create a BillingPaymentAllocation
                    // + run it through PaymentAllocationHelper.DistributeToChargesAsync exactly like a
                    // real payment — inflating each charge's "amount paid" and this invoice's total
                    // allocated-payments, so a later genuine payment could be wrongly capped/rejected
                    // as "already fully paid". CreateDraftInvoiceHandler already treats REFUND rows
                    // this same way (excluded from allocation, see its own comment there) — this just
                    // makes AddPaymentEventHandler consistent with that.
                }

                // Allocate the receipt number only after validation passes, so a rejected
                // (over/double) payment never burns a number in the series.
                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.Receipt, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var receiptNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix,
                    numberSeries.YearFormat,
                    numberSeries.Separator,
                    numberSeries.PadLength,
                    numberSeries.CurrentValue);

                var billingPayment = new BillingPayment
                {
                    PaymentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterId = request.EncounterId,
                    ReceiptNo = receiptNo,
                    PaymentType = normalizedPaymentType,
                    PaymentMode = request.Payment.PaymentMode,
                    PaymentDescription = request.Payment.Description,
                    TransactionId = request.Payment.TransactionId,
                    Amount = request.Payment.Amount,
                    PaidAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.LoggedInUserName
                };

                _context.BillingPayment.Add(billingPayment);

                if (allocatedAmount > 0)
                {
                    var paymentAllocation = new BillingPaymentAllocation
                    {
                        AllocationId = Guid.NewGuid(),
                        EncounterId = request.EncounterId,
                        PaymentId = billingPayment.PaymentId,
                        InvoiceId = billingInvoice.InvoiceId,
                        AllocatedAmount = allocatedAmount,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = request.LoggedInUserName
                    };

                    _context.BillingPaymentAllocation.Add(paymentAllocation);
                    await PaymentAllocationHelper.DistributeToChargesAsync(
                        _context, billingInvoice.InvoiceId, paymentAllocation.AllocationId, allocatedAmount, request.LoggedInUserName, cancellationToken);
                }

                numberSeries.UpdatedAt = DateTime.UtcNow;
                numberSeries.UpdatedBy = request.LoggedInUserName;
                // No explicit Update(): the entity is already tracked (modified if it existed, or
                // Added by the get-or-create helper) — an explicit Update would break the INSERT.

                await _context.SaveChangesAsync(cancellationToken);

                return new AddPaymentEventResponseModel
                {
                    Success = true,
                    Message = "Payment added successfully.",
                    Data = new AddPaymentData
                    {
                        PaymentId = billingPayment.PaymentId,
                        ReceiptNo = receiptNo,
                        AllocatedAmount = allocatedAmount,
                        CreditAmount = creditAmount > 0 ? creditAmount : null
                    }
                };
            }
            catch (Exception)
            {
                return new AddPaymentEventResponseModel
                {
                    Success = false,
                    Message = "Error adding payment."
                };
            }
        }

        // Deposit-before-any-charge: records the ADVANCE with no BillingPaymentAllocation (there's
        // no invoice yet to allocate against). CreateDraftInvoiceHandler picks up any unallocated
        // ADVANCE payments for the encounter and allocates them against the first real invoice.
        private async Task<AddPaymentEventResponseModel> RecordChargeLessAdvanceAsync(AddPaymentEventRequestModel request, CancellationToken cancellationToken)
        {
            var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                _context, request.HospitalId, BillingConstants.NumberSeriesCode.Receipt, request.LoggedInUserName, cancellationToken);
            numberSeries.CurrentValue++;
            var receiptNo = NumberSeriesFormatter.Format(
                numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);

            var now = DateTime.UtcNow;
            var billingPayment = new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                PatientId = request.PatientId,
                EncounterId = request.EncounterId,
                ReceiptNo = receiptNo,
                PaymentType = BillingConstants.PaymentType.Advance,
                PaymentMode = request.Payment!.PaymentMode,
                PaymentDescription = request.Payment.Description,
                TransactionId = request.Payment.TransactionId,
                Amount = request.Payment.Amount,
                PaidAt = now,
                CreatedAt = now,
                CreatedBy = request.LoggedInUserName,
                UpdatedAt = now,
                UpdatedBy = request.LoggedInUserName,
            };
            _context.BillingPayment.Add(billingPayment);

            numberSeries.UpdatedAt = now;
            numberSeries.UpdatedBy = request.LoggedInUserName;

            await _context.SaveChangesAsync(cancellationToken);

            return new AddPaymentEventResponseModel
            {
                Success = true,
                Message = "Deposit recorded — it will apply automatically once a charge is billed.",
                Data = new AddPaymentData
                {
                    PaymentId = billingPayment.PaymentId,
                    ReceiptNo = receiptNo,
                    AllocatedAmount = 0,
                    CreditAmount = request.Payment.Amount,
                }
            };
        }

    }
}
