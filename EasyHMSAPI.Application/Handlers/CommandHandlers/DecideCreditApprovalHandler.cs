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
    public class DecideCreditApprovalHandler : IRequestHandler<DecideCreditApprovalRequestModel, DecideCreditApprovalResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public DecideCreditApprovalHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<DecideCreditApprovalResponseModel> Handle(DecideCreditApprovalRequestModel request, CancellationToken cancellationToken)
        {
            var decision = (request.Decision ?? string.Empty).Trim().ToUpperInvariant();
            if (decision != "APPROVED" && decision != "REJECTED")
            {
                return new DecideCreditApprovalResponseModel { Success = false, Message = "Decision must be 'APPROVED' or 'REJECTED'." };
            }
            if (decision == "REJECTED" && string.IsNullOrWhiteSpace(request.DecisionNote))
            {
                return new DecideCreditApprovalResponseModel { Success = false, Message = "A reason is required to reject a credit request." };
            }

            var approval = await _context.CreditApproval
                .FirstOrDefaultAsync(a => a.CreditApprovalId == request.CreditApprovalId && a.HospitalId == request.HospitalId, cancellationToken);
            if (approval == null)
            {
                return new DecideCreditApprovalResponseModel { Success = false, Message = "Credit approval request not found." };
            }
            if (approval.Status != "PENDING")
            {
                return new DecideCreditApprovalResponseModel { Success = false, Message = $"This request has already been {approval.Status.ToLowerInvariant()}." };
            }

            var now = DateTime.UtcNow;

            if (decision == "APPROVED")
            {
                async Task<BillingInvoice?> LoadInvoiceAsync() => await _context.BillingInvoice
                    .Where(bi => bi.EncounterId == approval.EncounterId)
                    .OrderByDescending(bi => bi.StatusCode == BillingConstants.InvoiceStatus.Draft)
                    .ThenByDescending(bi => bi.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                var billingInvoice = await LoadInvoiceAsync();
                if (billingInvoice == null || billingInvoice.StatusCode == BillingConstants.InvoiceStatus.Draft)
                {
                    await _mediator.Send(new CreateDraftInvoiceRequestModel
                    {
                        PatientId = approval.PatientId,
                        EncounterId = approval.EncounterId,
                        HospitalId = approval.HospitalId,
                        LoggedInUserName = request.LoggedInUserName,
                    }, cancellationToken);
                    billingInvoice = await LoadInvoiceAsync();
                }
                if (billingInvoice == null)
                {
                    return new DecideCreditApprovalResponseModel { Success = false, Message = "No invoice could be created for this encounter." };
                }

                decimal allocatedAmount;
                if (approval.PaymentType == BillingConstants.PaymentType.Refund)
                {
                    // Refunds always fully allocate — they reduce the credit already held.
                    allocatedAmount = approval.RequestedAmount;
                }
                else
                {
                    var totalPastPayments = await _context.BillingPaymentAllocation
                        .Where(bpa => bpa.InvoiceId == billingInvoice.InvoiceId)
                        .SumAsync(bpa => bpa.AllocatedAmount, cancellationToken);
                    decimal remainingDue = (billingInvoice.NetAmount ?? 0) - totalPastPayments;
                    allocatedAmount = Math.Max(0, Math.Min(approval.RequestedAmount, remainingDue));
                }

                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, approval.HospitalId, BillingConstants.NumberSeriesCode.Receipt, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var receiptNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);

                var billingPayment = new BillingPayment
                {
                    PaymentId = Guid.NewGuid(),
                    HospitalId = approval.HospitalId,
                    PatientId = approval.PatientId,
                    EncounterId = approval.EncounterId,
                    ReceiptNo = receiptNo,
                    PaymentType = approval.PaymentType,
                    PaymentMode = approval.PaymentMode,
                    PaymentDescription = approval.PaymentDescription,
                    TransactionId = approval.TransactionId,
                    Amount = approval.RequestedAmount,
                    PaidAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.BillingPayment.Add(billingPayment);

                if (allocatedAmount > 0)
                {
                    _context.BillingPaymentAllocation.Add(new BillingPaymentAllocation
                    {
                        AllocationId = Guid.NewGuid(),
                        EncounterId = approval.EncounterId,
                        PaymentId = billingPayment.PaymentId,
                        InvoiceId = billingInvoice.InvoiceId,
                        AllocatedAmount = allocatedAmount,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                    });
                }

                numberSeries.UpdatedAt = now;
                numberSeries.UpdatedBy = request.LoggedInUserName;
            }

            approval.Status = decision;
            approval.DecidedAt = now;
            approval.DecidedBy = request.LoggedInUserName;
            approval.DecidedByUserId = request.LoggedInUserId;
            approval.DecisionNote = string.IsNullOrWhiteSpace(request.DecisionNote) ? null : request.DecisionNote.Trim();
            approval.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);

            return new DecideCreditApprovalResponseModel
            {
                Success = true,
                Message = decision == "APPROVED" ? "Credit approved and payment recorded." : "Credit request rejected.",
                Status = approval.Status,
                DecidedAt = approval.DecidedAt,
            };
        }
    }
}
