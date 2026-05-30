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

        public AddPaymentEventHandler(AppDbContext context)
        {
            _context = context;
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

                var numberSeries = await _context.NumberSeries
                    .Where(ns => ns.SeriesCode == BillingConstants.NumberSeriesCode.Receipt && ns.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (numberSeries == null)
                {
                    return new AddPaymentEventResponseModel
                    {
                        Success = false,
                        Message = "Receipt Number Series not configured for this hospital."
                    };
                }

                numberSeries.CurrentValue++;
                var receiptNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix,
                    numberSeries.YearFormat,
                    numberSeries.Separator,
                    numberSeries.PadLength,
                    numberSeries.CurrentValue);

                var billingInvoice = await _context.BillingInvoice
                    .Where(bi => bi.EncounterId == request.EncounterId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (billingInvoice == null)
                {
                    return new AddPaymentEventResponseModel
                    {
                        Success = false,
                        Message = "No billing invoice found for this encounter."
                    };
                }

                decimal netAmount = billingInvoice.NetAmount ?? 0;
                decimal allocatedAmount = 0;
                decimal creditAmount = 0;

                if (normalizedPaymentType == BillingConstants.PaymentType.Payment)
                {
                    if (request.Payment.Amount > netAmount)
                    {
                        return new AddPaymentEventResponseModel
                        {
                            Success = false,
                            Message = $"Payment amount ({request.Payment.Amount}) cannot exceed invoice net amount ({netAmount})."
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
                _context.NumberSeries.Update(numberSeries);

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
