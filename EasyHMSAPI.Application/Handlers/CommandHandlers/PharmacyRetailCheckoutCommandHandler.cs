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

                // A repeated Idempotency-Key (double click, retry, offline replay) returns the first sale: no
                // second stock issue, charge, invoice or payment.
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    var replay = await TryReplayAsync(request, cancellationToken);
                    if (replay != null)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return replay;
                    }
                }

                var cart = await ValidateCartAsync(request, cancellationToken);
                if (cart.Error != null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new PharmacyRetailCheckoutResponseModel { Success = false, Message = cart.Error };
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

                    // Server-side pricing: every allocated batch is charged at ITS OWN MRP, else the Charge Master
                    // default rate. The client's Rate is ignored, so a tampered or stale browser cannot sell below
                    // price (or at zero); the discount % was already validated against the allowed cap.
                    var invItem = cart.Items[item.InventoryItemId];
                    var master = cart.Masters[invItem.ChargeId!.Value];
                    var allocations = movementResponse.AllocatedBatchDetails;
                    var splitAcrossBatches = allocations.Count > 1;

                    if (allocations.Count == 0)
                    {
                        if (master.DefaultRate <= 0)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new PharmacyRetailCheckoutResponseModel { Success = false, Message = $"No price is configured for {invItem.ItemName}. Set a default rate in the Charge Master." };
                        }
                        chargeDetails.Add(BuildChargeDetail(invItem, item, item.Qty, master.DefaultRate, null, false));
                    }

                    foreach (var detail in allocations)
                    {
                        var price = detail.Mrp is > 0 ? detail.Mrp.Value : (master.DefaultRate > 0 ? master.DefaultRate : (decimal?)null);
                        if (price == null)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new PharmacyRetailCheckoutResponseModel { Success = false, Message = $"No price is configured for {invItem.ItemName} (set an MRP on batch {detail.BatchNumber} or a default rate in the Charge Master)." };
                        }
                        chargeDetails.Add(BuildChargeDetail(invItem, item, detail.AllocatedQty, price.Value, detail, splitAcrossBatches));
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
                    IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
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

                    // 5. Add Payment if applicable. The server owns the total: PayInFull collects exactly the invoice
                    // net; an explicit PaidAmount (credit / part payment) can never exceed it.
                    var amountToCollect = request.PayInFull ? netAmount : Math.Min(request.PaidAmount, netAmount);
                    if (amountToCollect > 0)
                    {
                        var paymentResponse = await _mediator.Send(new AddPaymentEventRequestModel
                        {
                            HospitalId = request.HospitalId,
                            PatientId = request.PatientId,
                            EncounterId = encounter.EncounterId,
                            Payment = new PaymentDetail
                            {
                                Amount = amountToCollect,
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

        private sealed class CartValidation
        {
            public string? Error { get; init; }
            public Dictionary<Guid, InventoryItem> Items { get; init; } = new();
            public Dictionary<Guid, ChargeMaster> Masters { get; init; } = new();
        }

        // Everything that can be rejected without touching stock: shape of the cart, items that belong to this
        // hospital, a charge for every item (an item with no charge would be dispensed free), and the discount
        // cap (ChargeMaster.MaxDiscountPercent, else the hospital's pharmacy default).
        private async Task<CartValidation> ValidateCartAsync(PharmacyRetailCheckoutCommand request, CancellationToken cancellationToken)
        {
            if (request.Items == null || request.Items.Count == 0)
                return new CartValidation { Error = "The cart is empty." };
            if (request.Items.Any(i => i.Qty <= 0))
                return new CartValidation { Error = "Quantity must be greater than zero on every line." };
            if (request.Items.Any(i => i.DiscountPercent < 0 || i.DiscountPercent > 100))
                return new CartValidation { Error = "Discount must be between 0 and 100 %." };

            var itemIds = request.Items.Select(i => i.InventoryItemId).Distinct().ToList();
            var items = await _context.InventoryItem
                .Where(i => i.HospitalId == request.HospitalId && itemIds.Contains(i.InventoryItemId))
                .ToDictionaryAsync(i => i.InventoryItemId, cancellationToken);
            if (items.Count != itemIds.Count)
                return new CartValidation { Error = "One or more items were not found." };

            var unbilled = items.Values.Where(i => !i.ChargeId.HasValue).Select(i => i.ItemName).ToList();
            if (unbilled.Count > 0)
                return new CartValidation { Error = $"These items have no charge configured and cannot be sold: {string.Join(", ", unbilled)}. Link them to a Charge Master entry first." };

            var chargeIds = items.Values.Select(i => i.ChargeId!.Value).Distinct().ToList();
            var masters = await _context.ChargeMaster
                .Where(m => m.HospitalId == request.HospitalId && chargeIds.Contains(m.ChargeId))
                .ToDictionaryAsync(m => m.ChargeId, cancellationToken);
            var missing = items.Values.Where(i => !masters.ContainsKey(i.ChargeId!.Value)).Select(i => i.ItemName).ToList();
            if (missing.Count > 0)
                return new CartValidation { Error = $"The Charge Master entry for {string.Join(", ", missing)} was not found." };

            var policyCap = await _context.BillingPolicy
                .Where(p => p.HospitalId == request.HospitalId)
                .Select(p => (decimal?)p.PharmacyMaxDiscountPercent)
                .FirstOrDefaultAsync(cancellationToken) ?? 20m;

            foreach (var line in request.Items)
            {
                var item = items[line.InventoryItemId];
                var cap = masters[item.ChargeId!.Value].MaxDiscountPercent ?? policyCap;
                if (line.DiscountPercent > cap)
                    return new CartValidation { Error = $"Discount on {item.ItemName} cannot exceed {cap:0.##} %." };
            }

            return new CartValidation { Items = items, Masters = masters };
        }

        private static ChargeDetail BuildChargeDetail(InventoryItem invItem, PharmacyCartItem line, decimal qty, decimal rate, AllocatedBatchDetail? batch, bool splitAcrossBatches) => new()
        {
            ChargeId = invItem.ChargeId,
            DisplayName = splitAcrossBatches && batch?.BatchNumber != null ? $"{invItem.ItemName} (Batch {batch.BatchNumber})" : invItem.ItemName,
            Qty = qty,
            Rate = rate,
            DiscountPercent = line.DiscountPercent,
            CategoryCode = invItem.Category,
            SourceModule = BillingConstants.SourceModule.PharmacyCounter,
            // The batch this line was dispensed from, so a later return can be tied to it.
            SourceRefId = batch?.BatchId.ToString(),
        };

        private async Task<PharmacyRetailCheckoutResponseModel?> TryReplayAsync(PharmacyRetailCheckoutCommand request, CancellationToken cancellationToken)
        {
            var key = request.IdempotencyKey!.Trim();
            var events = await _context.BillingChargeEvent
                .AsNoTracking()
                .Where(e => e.HospitalId == request.HospitalId && e.IdempotencyKey == key)
                .ToListAsync(cancellationToken);
            if (events.Count == 0) return null;

            var eventIds = events.Select(e => e.ChargeEventId).ToList();
            var invoice = await (from link in _context.BillingInvoiceChargeEvent
                                 join inv in _context.BillingInvoice on link.InvoiceId equals inv.InvoiceId
                                 where eventIds.Contains(link.ChargeEventId)
                                 select inv).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

            return new PharmacyRetailCheckoutResponseModel
            {
                Success = true,
                IsReplay = true,
                Message = "This sale was already recorded.",
                EncounterId = events[0].EncounterId,
                ChargeEventId = events[0].ChargeEventId,
                InvoiceId = invoice?.InvoiceId ?? Guid.Empty,
                InvoiceNo = invoice?.InvoiceNo,
            };
        }
    }
}
