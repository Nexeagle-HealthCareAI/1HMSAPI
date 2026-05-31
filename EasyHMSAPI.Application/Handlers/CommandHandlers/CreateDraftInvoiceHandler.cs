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
            decimal invoiceLevelDiscount = request.InvoiceDiscountAmount ?? 0;
            if (invoiceLevelDiscount < 0) invoiceLevelDiscount = 0;
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
