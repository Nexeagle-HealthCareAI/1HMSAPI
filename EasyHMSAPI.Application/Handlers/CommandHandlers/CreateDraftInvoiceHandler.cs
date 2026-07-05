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
    public class CreateDraftInvoiceHandler : IRequestHandler<CreateDraftInvoiceRequestModel, CreateDraftInvoiceResponseModel>
    {
        private readonly AppDbContext _context;
        private const int MaxConcurrencyRetries = 3;

        public CreateDraftInvoiceHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateDraftInvoiceResponseModel> Handle(CreateDraftInvoiceRequestModel request, CancellationToken cancellationToken)
        {
            // The DbContext is configured with EnableRetryOnFailure, so any user-initiated
            // transaction must run inside an execution strategy (as a retriable unit).
            var strategy = _context.Database.CreateExecutionStrategy();

            for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    return await strategy.ExecuteAsync(() => TryCreateAsync(request, cancellationToken));
                }
                catch (DbUpdateConcurrencyException)
                {
                    _context.ChangeTracker.Clear();
                    if (attempt == MaxConcurrencyRetries - 1)
                    {
                        return new CreateDraftInvoiceResponseModel
                        {
                            Success = false,
                            Message = "Invoice number allocation contention. Please retry."
                        };
                    }
                }
                catch (Exception)
                {
                    return new CreateDraftInvoiceResponseModel
                    {
                        Success = false,
                        Message = "Error creating draft invoice."
                    };
                }
            }

            return new CreateDraftInvoiceResponseModel
            {
                Success = false,
                Message = "Error creating draft invoice."
            };
        }

        private async Task<CreateDraftInvoiceResponseModel> TryCreateAsync(CreateDraftInvoiceRequestModel request, CancellationToken cancellationToken)
        {
            var encounter = await _context.Encounter
                .FirstOrDefaultAsync(e => e.EncounterId == request.EncounterId
                                       && e.HospitalId == request.HospitalId
                                       && e.PatientId == request.PatientId, cancellationToken);
            if (encounter == null)
            {
                return new CreateDraftInvoiceResponseModel { Success = false, Message = "Encounter not found." };
            }
            if (encounter.StatusCode != BillingConstants.EncounterStatus.Open)
            {
                return new CreateDraftInvoiceResponseModel
                {
                    Success = false,
                    Message = $"Encounter is not open (current status: {encounter.StatusCode})."
                };
            }

            var allChargeEvents = await _context.BillingChargeEvent
                .Where(ce => ce.EncounterId == request.EncounterId
                          && ce.HospitalId == request.HospitalId
                          && ce.StatusCode == BillingConstants.ChargeEventStatus.Posted)
                .ToListAsync(cancellationToken);
            if (allChargeEvents.Count == 0)
            {
                return new CreateDraftInvoiceResponseModel
                {
                    Success = false,
                    Message = "No posted charges available to invoice for this encounter."
                };
            }

            var alreadyLinkedIds = await _context.BillingInvoiceChargeEvent
                .Where(bice => allChargeEvents.Select(ce => ce.ChargeEventId).Contains(bice.ChargeEventId))
                .Select(bice => bice.ChargeEventId)
                .ToListAsync(cancellationToken);
            var alreadyLinkedSet = alreadyLinkedIds.ToHashSet();

            var unlinkedCharges = allChargeEvents
                .Where(ce => !alreadyLinkedSet.Contains(ce.ChargeEventId))
                .ToList();

            var existingDraft = await _context.BillingInvoice
                .Where(bi => bi.EncounterId == request.EncounterId
                          && bi.HospitalId == request.HospitalId
                          && bi.StatusCode == BillingConstants.InvoiceStatus.Draft)
                .FirstOrDefaultAsync(cancellationToken);

            var now = DateTime.UtcNow;
            bool wasReused = existingDraft != null;
            BillingInvoice invoice;
            string invoiceNo;

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            if (existingDraft != null)
            {
                invoice = existingDraft;
                invoiceNo = invoice.InvoiceNo ?? string.Empty;
            }
            else
            {
                if (unlinkedCharges.Count == 0)
                {
                    return new CreateDraftInvoiceResponseModel
                    {
                        Success = false,
                        Message = "All posted charges are already linked to an invoice."
                    };
                }

                // Use the hospital's invoice series, auto-creating it with platform defaults
                // (INV-YYYY-000001) when not yet configured — so numbering works for any hospital.
                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.Invoice, request.LoggedInUserName, cancellationToken);

                numberSeries.CurrentValue++;
                invoiceNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix,
                    numberSeries.YearFormat,
                    numberSeries.Separator,
                    numberSeries.PadLength,
                    numberSeries.CurrentValue);

                invoice = new BillingInvoice
                {
                    InvoiceId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterId = request.EncounterId,
                    InvoiceNo = invoiceNo,
                    InvoiceDate = now,
                    StatusCode = BillingConstants.InvoiceStatus.Draft,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName
                };
                _context.BillingInvoice.Add(invoice);

                numberSeries.UpdatedAt = now;
                numberSeries.UpdatedBy = request.LoggedInUserName;
            }

            foreach (var ce in unlinkedCharges)
            {
                _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent
                {
                    InvoiceId = invoice.InvoiceId,
                    ChargeEventId = ce.ChargeEventId
                });
            }

            var linkedChargeEvents = allChargeEvents
                .Where(ce => alreadyLinkedSet.Contains(ce.ChargeEventId) || unlinkedCharges.Contains(ce))
                .ToList();

            decimal gross = linkedChargeEvents.Sum(ce => ce.Qty * ce.UnitPrice);
            decimal lineDiscount = linkedChargeEvents.Sum(ce => ce.DiscountAmount ?? 0);

            // The discount already in effect, unaffected by this request — used both as the
            // "caller didn't specify one" fallback and as what stays in effect if a NEW discount
            // needs admin approval below.
            decimal existingInvoiceLevelDiscount;
            if (existingDraft != null)
            {
                decimal priorLineDiscount = allChargeEvents
                    .Where(ce => alreadyLinkedSet.Contains(ce.ChargeEventId))
                    .Sum(ce => ce.DiscountAmount ?? 0);
                existingInvoiceLevelDiscount = Math.Max(0, (existingDraft.DiscountAmount ?? 0) - priorLineDiscount);
            }
            else
            {
                existingInvoiceLevelDiscount = 0;
            }

            // Invoice-level (overall) discount. When the caller doesn't specify one, preserve the
            // existing one — keeps it intact when the draft is rebuilt to add a charge, record a
            // payment, or finalize, instead of silently resetting it to zero.
            decimal invoiceLevelDiscount = request.InvoiceDiscountAmount ?? existingInvoiceLevelDiscount;
            if (invoiceLevelDiscount < 0) invoiceLevelDiscount = 0;

            // A NEW discount request (not a payment/charge-triggered rebuild) that would reduce
            // NetAmount below money already collected needs admin sign-off — the same
            // CreditApproval gate used for advances/refunds — instead of silently discounting
            // money that's already been paid out.
            bool isExplicitDiscountRequest = request.InvoiceDiscountAmount.HasValue && !request.SkipCreditApprovalCheck;
            if (isExplicitDiscountRequest && existingDraft != null)
            {
                decimal requestedTotalDiscount = Math.Min(gross, lineDiscount + invoiceLevelDiscount);
                decimal requestedNet = gross - requestedTotalDiscount;
                decimal totalPaid = await _context.BillingPaymentAllocation
                    .Where(bpa => bpa.InvoiceId == invoice.InvoiceId)
                    .SumAsync(bpa => bpa.AllocatedAmount, cancellationToken);

                if (requestedNet < totalPaid)
                {
                    await tx.RollbackAsync(cancellationToken);
                    _context.ChangeTracker.Clear();

                    var approvalNow = DateTime.UtcNow;
                    var approval = new CreditApproval
                    {
                        CreditApprovalId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        EncounterId = request.EncounterId,
                        PatientId = request.PatientId,
                        PaymentType = "DISCOUNT",
                        RequestedAmount = request.InvoiceDiscountAmount!.Value,
                        ResultingCreditBalance = totalPaid - requestedNet,
                        Reason = "Discount would reduce the invoice below the amount already collected.",
                        RequestedBy = request.LoggedInUserName,
                        RequestedByUserId = request.LoggedInUserId,
                        RequestedAt = approvalNow,
                        Status = "PENDING",
                        CreatedAt = approvalNow,
                        UpdatedAt = approvalNow,
                    };
                    _context.CreditApproval.Add(approval);
                    await _context.SaveChangesAsync(cancellationToken);

                    return new CreateDraftInvoiceResponseModel
                    {
                        Success = true,
                        Message = $"This discount would leave {totalPaid - requestedNet:0.00} of already-collected money unaccounted for and requires admin approval before it's applied.",
                        PendingApproval = true,
                        CreditApprovalId = approval.CreditApprovalId,
                    };
                }
            }

            decimal totalDiscount = lineDiscount + invoiceLevelDiscount;
            if (totalDiscount > gross) totalDiscount = gross;
            decimal net = gross - totalDiscount;

            // GST roll-up (per-line snapshots already computed at post time)
            decimal taxable = linkedChargeEvents.Sum(ce => ce.TaxableAmount ?? ce.NetAmount);
            decimal cgst = linkedChargeEvents.Sum(ce => ce.CgstAmount);
            decimal sgst = linkedChargeEvents.Sum(ce => ce.SgstAmount);
            decimal igst = linkedChargeEvents.Sum(ce => ce.IgstAmount);
            decimal tax  = linkedChargeEvents.Sum(ce => ce.TaxAmount);

            invoice.GrossAmount = gross;
            invoice.DiscountAmount = totalDiscount;
            invoice.NetAmount = net;
            invoice.TaxableAmount = taxable;
            invoice.CgstAmount = cgst;
            invoice.SgstAmount = sgst;
            invoice.IgstAmount = igst;
            invoice.TaxAmount = tax;
            invoice.UpdatedAt = now;
            invoice.UpdatedBy = request.LoggedInUserName;

            // Flush the charge-invoice links created above before querying them back below —
            // PaymentAllocationHelper reads BillingInvoiceChargeEvent from the database, and on a
            // first-time invoice those links were only added to the change tracker, not yet saved.
            await _context.SaveChangesAsync(cancellationToken);

            // Auto-allocate any unallocated payment balance for this encounter against this
            // invoice's remaining due — covers a charge-less deposit taken before this invoice
            // existed (AddPaymentEventHandler's charge-less ADVANCE path) AND money freed up by
            // cancelling a paid charge elsewhere (DeleteBillingEventHandler reverses that charge's
            // BillingPaymentAllocationCharge share, leaving the parent payment partly unallocated).
            // REFUND rows are excluded — that's money paid back out, never available credit.
            var unallocatedCandidatePayments = await _context.BillingPayment
                .Where(p => p.EncounterId == request.EncounterId && p.PaymentType != BillingConstants.PaymentType.Refund)
                .OrderBy(p => p.PaidAt)
                .ToListAsync(cancellationToken);
            if (unallocatedCandidatePayments.Count > 0)
            {
                var candidatePaymentIds = unallocatedCandidatePayments.Select(p => p.PaymentId).ToList();
                var allocatedByPayment = await _context.BillingPaymentAllocation
                    .Where(a => candidatePaymentIds.Contains(a.PaymentId))
                    .GroupBy(a => a.PaymentId)
                    .Select(g => new { PaymentId = g.Key, Total = g.Sum(x => x.AllocatedAmount) })
                    .ToDictionaryAsync(x => x.PaymentId, x => x.Total, cancellationToken);
                decimal alreadyAllocatedToThisInvoice = await _context.BillingPaymentAllocation
                    .Where(a => a.InvoiceId == invoice.InvoiceId)
                    .SumAsync(a => a.AllocatedAmount, cancellationToken);
                decimal remainingDue = net - alreadyAllocatedToThisInvoice;

                foreach (var payment in unallocatedCandidatePayments)
                {
                    if (remainingDue <= 0) break;
                    var alreadyAllocated = allocatedByPayment.TryGetValue(payment.PaymentId, out var a) ? a : 0m;
                    var available = payment.Amount - alreadyAllocated;
                    if (available <= 0) continue;
                    var toAllocate = Math.Min(available, remainingDue);
                    var newAllocation = new BillingPaymentAllocation
                    {
                        AllocationId = Guid.NewGuid(),
                        EncounterId = request.EncounterId,
                        PaymentId = payment.PaymentId,
                        InvoiceId = invoice.InvoiceId,
                        AllocatedAmount = toAllocate,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                    };
                    _context.BillingPaymentAllocation.Add(newAllocation);
                    await PaymentAllocationHelper.DistributeToChargesAsync(
                        _context, invoice.InvoiceId, newAllocation.AllocationId, toAllocate, request.LoggedInUserName, cancellationToken);
                    remainingDue -= toAllocate;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new CreateDraftInvoiceResponseModel
            {
                Success = true,
                Message = wasReused
                    ? $"Linked {unlinkedCharges.Count} additional charge(s) to existing draft invoice."
                    : $"Draft invoice created with {unlinkedCharges.Count} charge(s).",
                Data = new CreateDraftInvoiceData
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNo = invoiceNo,
                    EncounterId = request.EncounterId,
                    LinkedChargeCount = linkedChargeEvents.Count,
                    GrossAmount = gross,
                    DiscountAmount = totalDiscount,
                    NetAmount = net,
                    TaxableAmount = taxable,
                    CgstAmount = cgst,
                    SgstAmount = sgst,
                    IgstAmount = igst,
                    TaxAmount = tax,
                    WasReused = wasReused
                }
            };
        }
    }
}
