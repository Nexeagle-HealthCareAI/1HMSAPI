using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Patient return/restock. Every line is tied to a real charge event of THIS invoice, the refund price is
    // taken from that charge event (never from the request), and the quantity is capped by what that charge
    // actually sold minus what was already returned. Stock goes back to the original batch through the shared
    // movement handler (MovementType=RETURN).
    //
    // Money: the original charge event is never edited (BillingChargeEvent has no partial-quantity adjustment),
    // so each return posts a REVERSING charge event (negative quantity and amounts, linked to the original by
    // SourceRefId, same pharmacy source module). That makes sales / ABC / GST analytics and the encounter's
    // billed total net of returns. If the customer has paid more than the reduced bill, the difference is paid
    // back as a REFUND payment through AddPaymentEvent (a part-paid credit sale only reduces what is still due).
    public class CreatePharmacyReturnHandler : IRequestHandler<CreatePharmacyReturnRequestModel, CreatePharmacyReturnResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<CreatePharmacyReturnHandler>? _logger;

        public CreatePharmacyReturnHandler(AppDbContext context, IMediator mediator, ILogger<CreatePharmacyReturnHandler>? logger = null)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<CreatePharmacyReturnResponseModel> Handle(CreatePharmacyReturnRequestModel request, CancellationToken cancellationToken)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => TryHandleAsync(request, cancellationToken));
        }

        private async Task<CreatePharmacyReturnResponseModel> TryHandleAsync(CreatePharmacyReturnRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.InvoiceNo))
                return new CreatePharmacyReturnResponseModel { Success = false, Message = "HospitalId and InvoiceNo are required." };
            if (request.Lines == null || request.Lines.Count == 0)
                return new CreatePharmacyReturnResponseModel { Success = false, Message = "At least one return line is required." };
            if (request.Lines.Any(l => l.ReturnedQty <= 0))
                return new CreatePharmacyReturnResponseModel { Success = false, Message = "Returned quantity must be greater than zero on every line." };

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var invoice = await _context.BillingInvoice.FirstOrDefaultAsync(
                    i => i.HospitalId == request.HospitalId && i.InvoiceNo == request.InvoiceNo.Trim(), cancellationToken);
                if (invoice == null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new CreatePharmacyReturnResponseModel { Success = false, Message = "Invoice not found." };
                }

                // The charge events that were actually billed on THIS invoice (and are still live).
                var lineEventIds = request.Lines.Select(l => l.ChargeEventId).Distinct().ToList();
                var invoiceEvents = await (from link in _context.BillingInvoiceChargeEvent
                                           join ce in _context.BillingChargeEvent on link.ChargeEventId equals ce.ChargeEventId
                                           where link.InvoiceId == invoice.InvoiceId
                                                 && lineEventIds.Contains(ce.ChargeEventId)
                                                 && ce.StatusCode != BillingConstants.ChargeEventStatus.Void
                                           select ce).ToDictionaryAsync(ce => ce.ChargeEventId, cancellationToken);

                var batchIds = request.Lines.Select(l => l.BatchId).Distinct().ToList();
                var batches = await _context.Batch.Where(b => b.HospitalId == request.HospitalId && batchIds.Contains(b.BatchId)).ToDictionaryAsync(b => b.BatchId, cancellationToken);
                var itemIds = request.Lines.Select(l => l.InventoryItemId).Distinct().ToList();
                var itemChargeIds = await _context.InventoryItem
                    .Where(i => i.HospitalId == request.HospitalId && itemIds.Contains(i.InventoryItemId))
                    .ToDictionaryAsync(i => i.InventoryItemId, i => i.ChargeId, cancellationToken);

                var today = DateTime.UtcNow.Date;
                var now = DateTime.UtcNow;
                decimal totalRefund = 0;
                var returnLines = new List<PharmacyReturnLine>();
                var reversals = new List<BillingChargeEvent>();
                // The same charge event may appear on several lines of one request: count it once.
                var requestedByEvent = new Dictionary<Guid, decimal>();

                foreach (var line in request.Lines)
                {
                    if (!invoiceEvents.TryGetValue(line.ChargeEventId, out var original))
                        return await FailAsync(tx, "One or more lines do not belong to this invoice.", cancellationToken);
                    if (!itemChargeIds.TryGetValue(line.InventoryItemId, out var chargeId) || chargeId == null || original.ChargeId != chargeId)
                        return await FailAsync(tx, "A returned item does not match the invoice line.", cancellationToken);
                    if (!batches.TryGetValue(line.BatchId, out var batch) || batch.InventoryItemId != line.InventoryItemId)
                        return await FailAsync(tx, "One or more batches were not found.", cancellationToken);
                    // Events posted by the new checkout carry the dispensed batch in SourceRefId; hold the line to it.
                    if (Guid.TryParse(original.SourceRefId, out var soldBatchId) && soldBatchId != line.BatchId)
                        return await FailAsync(tx, "The returned batch does not match what was dispensed for this invoice line.", cancellationToken);
                    if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < today)
                        return await FailAsync(tx, $"Batch {batch.BatchNumber} is expired and cannot be restocked.", cancellationToken);
                    if (original.Qty <= 0)
                        return await FailAsync(tx, "This invoice line cannot be returned.", cancellationToken);

                    // Returnable = what this charge sold (capped by what the stock ledger shows was issued from
                    // the batch for the encounter) minus everything already returned against this charge event.
                    var dispensedQty = await _context.InventoryMovement.AsNoTracking()
                        .Where(m => m.HospitalId == request.HospitalId && m.EncounterId == invoice.EncounterId
                                 && m.MovementType == "ISSUE" && m.BatchId == line.BatchId && m.InventoryItemId == line.InventoryItemId)
                        .SumAsync(m => (decimal?)m.Qty, cancellationToken) ?? 0m;
                    var alreadyReturned = await _context.PharmacyReturnLine.AsNoTracking()
                        .Where(l => l.ChargeEventId == line.ChargeEventId)
                        .SumAsync(l => (decimal?)l.ReturnedQty, cancellationToken) ?? 0m;
                    requestedByEvent[line.ChargeEventId] = requestedByEvent.GetValueOrDefault(line.ChargeEventId) + line.ReturnedQty;
                    var returnable = Math.Min(original.Qty, dispensedQty) - alreadyReturned;
                    if (requestedByEvent[line.ChargeEventId] > returnable)
                        return await FailAsync(tx, $"Cannot return {line.ReturnedQty} from batch {batch.BatchNumber} — only {Math.Max(0, returnable)} remains returnable.", cancellationToken);

                    // Restock the exact batch it came from, through the shared movement handler.
                    var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                    {
                        HospitalId = request.HospitalId,
                        InventoryItemId = line.InventoryItemId,
                        BatchId = line.BatchId,
                        MovementType = IpdConstants.InventoryMovementType.Return,
                        Qty = line.ReturnedQty,
                        EncounterId = invoice.EncounterId,
                        PatientId = invoice.PatientId,
                        SourceModule = "PHARMACY_RETURN",
                        LoggedInUserId = request.LoggedInUserId,
                        LoggedInUserName = request.LoggedInUserName,
                    }, cancellationToken);
                    if (!movementResponse.Success)
                        return await FailAsync(tx, $"Stock reversal failed: {movementResponse.Message}", cancellationToken);

                    // Refund what was really charged per unit (net of the discount actually given), never what
                    // the client says.
                    var share = line.ReturnedQty / original.Qty;
                    decimal Part(decimal? amount) => Math.Round((amount ?? 0m) * share, 2);
                    var refundAmount = Part(original.NetAmount);
                    var unitRefund = Math.Round(original.NetAmount / original.Qty, 4);
                    totalRefund += refundAmount;

                    returnLines.Add(new PharmacyReturnLine
                    {
                        ReturnLineId = Guid.NewGuid(),
                        ChargeEventId = line.ChargeEventId,
                        InventoryItemId = line.InventoryItemId,
                        BatchId = line.BatchId,
                        ReturnedQty = line.ReturnedQty,
                        UnitPrice = unitRefund,
                        RefundAmount = refundAmount,
                    });

                    // CK_BCE_Discount forbids a negative discount, so the reversal carries the NET value as its
                    // gross with no discount: net, taxable and tax come out negative and net sales/GST correctly.
                    reversals.Add(new BillingChargeEvent
                    {
                        ChargeEventId = Guid.NewGuid(),
                        HospitalId = original.HospitalId,
                        PatientId = original.PatientId,
                        EncounterId = original.EncounterId,
                        ChargeId = original.ChargeId,
                        SourceModule = original.SourceModule,
                        SourceRefId = original.ChargeEventId.ToString(),
                        CategoryCode = original.CategoryCode,
                        DisplayName = $"Return: {original.DisplayName}",
                        Qty = -line.ReturnedQty,
                        UnitPrice = original.UnitPrice,
                        GrossAmount = -refundAmount,
                        DiscountAmount = 0m,
                        NetAmount = -refundAmount,
                        HsnSacCode = original.HsnSacCode,
                        GstRate = original.GstRate,
                        TaxableAmount = -Part(original.TaxableAmount),
                        CgstAmount = -Part(original.CgstAmount),
                        SgstAmount = -Part(original.SgstAmount),
                        IgstAmount = -Part(original.IgstAmount),
                        TaxAmount = -Part(original.TaxAmount),
                        IsTaxInclusive = original.IsTaxInclusive,
                        IsInterState = original.IsInterState,
                        StatusCode = BillingConstants.ChargeEventStatus.Posted,
                        ServiceDate = now,
                        PostedAt = now,
                        PostedBy = request.LoggedInUserName,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName,
                    });
                }

                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.PharmacyReturn, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var returnNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);

                var pharmacyReturn = new PharmacyReturn
                {
                    ReturnId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    InvoiceId = invoice.InvoiceId,
                    InvoiceNo = invoice.InvoiceNo,
                    PatientId = invoice.PatientId,
                    EncounterId = invoice.EncounterId,
                    ReturnNo = returnNo,
                    TotalRefundAmount = totalRefund,
                    RefundMode = request.RefundMode,
                    Notes = request.Notes,
                    ReturnedAt = now,
                    ReturnedBy = request.LoggedInUserName,
                    ReturnedByUserId = request.LoggedInUserId,
                    CreatedAt = now,
                };
                _context.PharmacyReturn.Add(pharmacyReturn);

                // Saved before the lines: PharmacyReturnLine.ReturnId is a plain FK column, not an
                // EF navigation property, so nothing tells SaveChangesAsync to insert the parent
                // first — without this, real SQL Server intermittently inserts a line before its
                // return and trips FK_PHRETL_Return (same class of bug as the GRN/Batch ordering fix).
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var rl in returnLines)
                {
                    rl.ReturnId = pharmacyReturn.ReturnId;
                    _context.PharmacyReturnLine.Add(rl);
                }
                _context.BillingChargeEvent.AddRange(reversals);
                await _context.SaveChangesAsync(cancellationToken);

                // Pay back only what the customer has actually paid in excess of the reduced bill.
                var cashRefund = await ComputeRefundableCashAsync(invoice.EncounterId, totalRefund, cancellationToken);
                if (cashRefund > 0)
                {
                    var paymentResponse = await _mediator.Send(new AddPaymentEventRequestModel
                    {
                        HospitalId = request.HospitalId,
                        PatientId = invoice.PatientId,
                        EncounterId = invoice.EncounterId,
                        Payment = new PaymentDetail
                        {
                            Amount = cashRefund,
                            PaymentMode = string.IsNullOrWhiteSpace(request.RefundMode) ? "CASH" : request.RefundMode,
                            PaymentType = BillingConstants.PaymentType.Refund,
                            Description = $"Pharmacy return {returnNo} against {invoice.InvoiceNo}",
                        },
                        LoggedInUserId = request.LoggedInUserId,
                        LoggedInUserName = request.LoggedInUserName,
                    }, cancellationToken);
                    if (paymentResponse.Success != true)
                        return await FailAsync(tx, $"Refund payment failed: {paymentResponse.Message}", cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);

                return new CreatePharmacyReturnResponseModel
                {
                    Success = true,
                    Message = "Return recorded.",
                    ReturnId = pharmacyReturn.ReturnId,
                    ReturnNo = returnNo,
                    TotalRefundAmount = totalRefund,
                    CashRefundAmount = cashRefund,
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger?.LogError(ex, "Pharmacy return failed for invoice {InvoiceNo}", request.InvoiceNo);
                return new CreatePharmacyReturnResponseModel { Success = false, Message = "An error occurred while recording the return." };
            }
        }

        // Credit the customer is owed after the reversals: collected minus refunded minus the (already reduced)
        // billed total, never more than the value just returned.
        private async Task<decimal> ComputeRefundableCashAsync(Guid? encounterId, decimal returnedValue, CancellationToken cancellationToken)
        {
            if (encounterId == null) return 0m;
            var collected = await _context.BillingPayment
                .Where(p => p.EncounterId == encounterId && (p.PaymentType == BillingConstants.PaymentType.Payment || p.PaymentType == BillingConstants.PaymentType.Advance))
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
            var refunded = await _context.BillingPayment
                .Where(p => p.EncounterId == encounterId && p.PaymentType == BillingConstants.PaymentType.Refund)
                .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
            var billed = await _context.BillingChargeEvent
                .Where(c => c.EncounterId == encounterId && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                .SumAsync(c => (decimal?)c.NetAmount, cancellationToken) ?? 0m;
            var credit = Math.Max(0m, (collected - refunded) - billed);
            return Math.Round(Math.Min(credit, returnedValue), 2);
        }

        private async Task<CreatePharmacyReturnResponseModel> FailAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, string message, CancellationToken cancellationToken)
        {
            await tx.RollbackAsync(cancellationToken);
            return new CreatePharmacyReturnResponseModel { Success = false, Message = message };
        }
    }
}
