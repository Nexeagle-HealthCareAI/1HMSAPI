using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class PharmacyRetailCheckoutCommandHandler : IRequestHandler<PharmacyRetailCheckoutCommand, PharmacyRetailCheckoutResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<PharmacyRetailCheckoutCommandHandler> _logger;
        private readonly IUsageLimitService _usageLimitService;

        public PharmacyRetailCheckoutCommandHandler(AppDbContext context, IMediator mediator, ILogger<PharmacyRetailCheckoutCommandHandler> logger, IUsageLimitService usageLimitService)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
            _usageLimitService = usageLimitService;
        }

        public async Task<PharmacyRetailCheckoutResponseModel> Handle(PharmacyRetailCheckoutCommand request, CancellationToken cancellationToken)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => TryHandleAsync(request, cancellationToken));
        }

        private async Task<PharmacyRetailCheckoutResponseModel> TryHandleAsync(PharmacyRetailCheckoutCommand request, CancellationToken cancellationToken)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var postToAdmissionDayBill = request.SettlementMode == PharmacySettlementMode.PostToAdmissionDayBill;

                // Every dispense — cash sale or admission-billed — must be tied to a real,
                // searched-or-registered PatientRegistration. Previously only enforced for the
                // admission path; a plain cash sale could go out with no patient at all, leaving
                // regulated (Schedule H/H1/X) drugs with no traceable recipient.
                if (string.IsNullOrWhiteSpace(request.PatientId))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new PharmacyRetailCheckoutResponseModel { Success = false, Message = "A patient is required to dispense medicine." };
                }

                Encounter encounter;
                if (postToAdmissionDayBill)
                {
                    var admission = await _context.Admission
                        .Where(a => a.HospitalId == request.HospitalId && a.PatientId == request.PatientId && a.StatusCode == IpdConstants.AdmissionStatus.Admitted && a.EncounterId != null)
                        .OrderByDescending(a => a.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (admission?.EncounterId == null)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new PharmacyRetailCheckoutResponseModel { Success = false, Message = "No active admission found for this patient — cannot post to admission day bill." };
                    }

                    encounter = await _context.Encounter.FirstAsync(e => e.EncounterId == admission.EncounterId, cancellationToken);
                }
                else
                {
                    // 1. Create Pharmacy Encounter
                    encounter = new Encounter
                    {
                        EncounterId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        PatientId = request.PatientId,
                        EncounterTypeCode = AppConstants.VisitType_PHARMACY,
                        SourceType = "RETAIL_WALKIN",
                        PrimaryDoctorId = request.PrescribingDoctorId,
                        StatusCode = BillingConstants.EncounterStatus.Open,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName ?? "System",
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName ?? "System"
                    };
                    _context.Encounter.Add(encounter);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // 2. Issue Stock & Create Charges
                var chargeDetails = new List<ChargeDetail>();
                var allocatedBatches = new List<AllocatedBatchLine>();

                foreach (var item in request.Items)
                {
                    // Issue Stock via MediatR (handles batching, expiry checks, etc.)
                    var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                    {
                        HospitalId = request.HospitalId,
                        InventoryItemId = item.InventoryItemId,
                        StoreId = request.StoreId,
                        BatchId = item.BatchId,
                        MovementType = "ISSUE",
                        Qty = item.Qty,
                        EncounterId = encounter.EncounterId,
                        PatientId = request.PatientId,
                        SourceModule = BillingConstants.SourceModule.PharmacyCounter,
                        PrescriberRef = request.PrescriberRef,
                        LoggedInUserId = request.LoggedInUserId,
                        LoggedInUserName = request.LoggedInUserName
                    }, cancellationToken);

                    if (!movementResponse.Success)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new PharmacyRetailCheckoutResponseModel { Success = false, Message = $"Stock issue failed: {movementResponse.Message}" };
                    }

                    foreach (var detail in movementResponse.AllocatedBatchDetails)
                    {
                        allocatedBatches.Add(new AllocatedBatchLine
                        {
                            InventoryItemId = item.InventoryItemId,
                            BatchId = detail.BatchId,
                            BatchNumber = detail.BatchNumber,
                            ExpiryDate = detail.ExpiryDate,
                            Mrp = detail.Mrp,
                            AllocatedQty = detail.AllocatedQty
                        });
                    }

                    // Look up ChargeId
                    var invItem = await _context.InventoryItem.FindAsync(new object[] { item.InventoryItemId }, cancellationToken);
                    if (invItem?.ChargeId != null)
                    {
                        chargeDetails.Add(new ChargeDetail
                        {
                            ChargeId = invItem.ChargeId,
                            DisplayName = invItem.ItemName,
                            Qty = item.Qty,
                            Rate = item.Rate,
                            DiscountPercent = item.DiscountPercent,
                            CategoryCode = invItem.Category,
                            SourceModule = BillingConstants.SourceModule.PharmacyCounter
                        });
                    }
                }

                if (!chargeDetails.Any())
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new PharmacyRetailCheckoutResponseModel { Success = false, Message = "No billable items in cart." };
                }

                // 3. Post Charges
                var chargeResponse = await _mediator.Send(new AddChargeEventRequestModel
                {
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterId = encounter.EncounterId,
                    Charges = chargeDetails,
                    LoggedInUserId = request.LoggedInUserId,
                    LoggedInUserName = request.LoggedInUserName
                }, cancellationToken);

                if (chargeResponse.Success != true || chargeResponse.Data?.ChargeEvents == null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new PharmacyRetailCheckoutResponseModel { Success = false, Message = $"Billing failed: {chargeResponse.Message}" };
                }

                var chargeEvents = await _context.BillingChargeEvent
                    .Where(ce => chargeResponse.Data.ChargeEvents.Select(c => c.ChargeEventId).Contains(ce.ChargeEventId))
                    .ToListAsync(cancellationToken);

                Guid invoiceIdResult = Guid.Empty;
                string? invoiceNo = null;

                if (postToAdmissionDayBill)
                {
                    // Charges are posted against the admission's Encounter and left un-invoiced —
                    // CloseAdmissionDayHandler snapshots them into AdmissionDayBillLine on the next
                    // day-close, same as any other ward/pathology/OT charge. No BillingInvoice or
                    // payment is created here; IPD settlement happens at day-close/discharge.
                }
                else
                {
                    // 4. Create Finalized Invoice
                    decimal netAmount = chargeEvents.Sum(c => c.NetAmount);
                    decimal taxAmount = chargeEvents.Sum(c => c.TaxAmount);

                    var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                        _context, request.HospitalId, BillingConstants.NumberSeriesCode.Invoice, request.LoggedInUserName, cancellationToken);

                    numberSeries.CurrentValue++;
                    invoiceNo = NumberSeriesFormatter.Format(
                        numberSeries.Prefix,
                        numberSeries.YearFormat,
                        numberSeries.Separator,
                        numberSeries.PadLength,
                        numberSeries.CurrentValue);

                    var invoice = new BillingInvoice
                    {
                        InvoiceId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        PatientId = request.PatientId,
                        EncounterId = encounter.EncounterId,
                        InvoiceNo = invoiceNo,
                        GrossAmount = chargeEvents.Sum(c => c.GrossAmount ?? 0),
                        DiscountAmount = chargeEvents.Sum(c => c.DiscountAmount ?? 0),
                        TaxAmount = taxAmount,
                        NetAmount = netAmount,
                        StatusCode = BillingConstants.InvoiceStatus.Finalized,
                        InvoiceDate = now,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName ?? "System",
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName ?? "System"
                    };

                    _context.BillingInvoice.Add(invoice);
                    invoiceIdResult = invoice.InvoiceId;
                    // Saved before the link rows: BillingInvoiceChargeEvent.InvoiceId is a plain FK
                    // column, not an EF navigation property, so nothing tells SaveChangesAsync to
                    // insert the invoice first — without this, real SQL Server can insert a link row
                    // before its invoice and trip FK_BICE_Invoice (same class of bug as the
                    // PharmacyReturn/VendorReturn ordering fix, missed here since this is the one
                    // pharmacy handler that builds both in a single SaveChanges call).
                    await _context.SaveChangesAsync(cancellationToken);

                    foreach (var ce in chargeEvents)
                    {
                        _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent
                        {
                            InvoiceId = invoice.InvoiceId,
                            ChargeEventId = ce.ChargeEventId
                        });
                    }

                    await _context.SaveChangesAsync(cancellationToken);

                    // 5. Add Payment if applicable
                    if (request.PaidAmount > 0)
                    {
                        var paymentResponse = await _mediator.Send(new AddPaymentEventRequestModel
                        {
                            HospitalId = request.HospitalId,
                            PatientId = request.PatientId,
                            EncounterId = encounter.EncounterId,
                            Payment = new PaymentDetail
                            {
                                Amount = request.PaidAmount,
                                PaymentMode = request.PaymentMode ?? "CASH",
                                PaymentType = BillingConstants.PaymentType.Payment,
                                Description = "Retail Pharmacy POS Payment"
                            },
                            LoggedInUserId = request.LoggedInUserId,
                            LoggedInUserName = request.LoggedInUserName
                        }, cancellationToken);

                        if (paymentResponse.Success != true)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new PharmacyRetailCheckoutResponseModel { Success = false, Message = $"Payment failed: {paymentResponse.Message}" };
                        }
                    }

                    // Close the Encounter (only for a standalone retail encounter — an admission's
                    // Encounter stays open/managed by the IPD workflow).
                    encounter.StatusCode = BillingConstants.EncounterStatus.Finalized;
                    _context.Encounter.Update(encounter);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // Last gate before commit -- a free-tier hospital's monthly quota, atomically
                // checked and consumed together with this checkout inside the same transaction,
                // so a limit breach here rolls the whole checkout back too.
                var usage = await _usageLimitService.TryConsumeAsync(request.HospitalId, cancellationToken);
                if (!usage.Allowed)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new PharmacyRetailCheckoutResponseModel { Success = false, Message = usage.Message };
                }

                await tx.CommitAsync(cancellationToken);

                return new PharmacyRetailCheckoutResponseModel
                {
                    Success = true,
                    AllocatedBatches = allocatedBatches,
                    EncounterId = encounter.EncounterId,
                    InvoiceId = invoiceIdResult,
                    InvoiceNo = invoiceNo,
                    ChargeEventId = chargeEvents.First().ChargeEventId
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Pharmacy Retail Checkout failed.");
                return new PharmacyRetailCheckoutResponseModel { Success = false, Message = "An error occurred during checkout." };
            }
        }
    }
}
