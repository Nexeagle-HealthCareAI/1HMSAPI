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
                    var totalPastPayments = await _context.BillingPaymentAllocation
                        .Where(bpa => bpa.InvoiceId == billingInvoice.InvoiceId)
                        .SumAsync(bpa => bpa.AllocatedAmount, cancellationToken);

                    decimal availableCredit = Math.Max(0, totalPastPayments - netAmount);

                    if (availableCredit <= 0 || request.Payment.Amount > availableCredit)
                    {
                        return new AddPaymentEventResponseModel
                        {
                            Success = false,
                            Message = $"Insufficient credit available. Available credit: {availableCredit}"
                        };
                    }

                    allocatedAmount = request.Payment.Amount;
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
    }
}
