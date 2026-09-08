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
    // Patient return/restock: validates each line against what's actually still returnable
    // (dispensed minus already-returned, batch not expired), reverses stock for real via the
    // shared movement handler (MovementType=RETURN), and records the return + refund amount in
    // its own ledger — the original invoice/BillingChargeEvent rows are never touched (see
    // PHARMACY_PRD Phase 3d notes on why: no partial-qty adjustment primitive exists on
    // BillingChargeEvent today).
    public class CreatePharmacyReturnHandler : IRequestHandler<CreatePharmacyReturnRequestModel, CreatePharmacyReturnResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public CreatePharmacyReturnHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
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

                var batchIds = request.Lines.Select(l => l.BatchId).Distinct().ToList();
                var batches = await _context.Batch.Where(b => batchIds.Contains(b.BatchId)).ToDictionaryAsync(b => b.BatchId, cancellationToken);

                var today = DateTime.UtcNow.Date;
                var now = DateTime.UtcNow;
                decimal totalRefund = 0;
                var returnLines = new List<PharmacyReturnLine>();

                foreach (var line in request.Lines)
                {
                    if (!batches.TryGetValue(line.BatchId, out var batch))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new CreatePharmacyReturnResponseModel { Success = false, Message = "One or more batches were not found." };
                    }
                    if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < today)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new CreatePharmacyReturnResponseModel { Success = false, Message = $"Batch {batch.BatchNumber} is expired and cannot be restocked." };
                    }

                    // Re-validate the returnable ceiling server-side (the caller's own preview grid
                    // could be stale) — dispensed-for-this-line minus what's already been returned.
                    var dispensedQty = await _context.InventoryMovement.AsNoTracking()
                        .Where(m => m.HospitalId == request.HospitalId && m.EncounterId == invoice.EncounterId
                                 && m.MovementType == "ISSUE" && m.BatchId == line.BatchId && m.InventoryItemId == line.InventoryItemId)
                        .SumAsync(m => (decimal?)m.Qty, cancellationToken) ?? 0m;

                    var alreadyReturned = await _context.PharmacyReturnLine.AsNoTracking()
                        .Where(l => l.ChargeEventId == line.ChargeEventId && l.BatchId == line.BatchId)
                        .SumAsync(l => (decimal?)l.ReturnedQty, cancellationToken) ?? 0m;

                    var returnable = dispensedQty - alreadyReturned;
                    if (line.ReturnedQty > returnable)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new CreatePharmacyReturnResponseModel
                        {
                            Success = false,
                            Message = $"Cannot return {line.ReturnedQty} from batch {batch.BatchNumber} — only {returnable} remains returnable."
                        };
                    }

                    // Restock — same shared movement handler every other stock change uses.
                    // No StoreId passed: the batch's own StoreId is used as the destination, so
                    // this always lands the units back in the exact batch they came from.
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
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new CreatePharmacyReturnResponseModel { Success = false, Message = $"Stock reversal failed: {movementResponse.Message}" };
                    }

                    var refundAmount = Math.Round(line.UnitPrice * line.ReturnedQty, 2);
                    totalRefund += refundAmount;

                    returnLines.Add(new PharmacyReturnLine
                    {
                        ReturnLineId = Guid.NewGuid(),
                        ChargeEventId = line.ChargeEventId,
                        InventoryItemId = line.InventoryItemId,
                        BatchId = line.BatchId,
                        ReturnedQty = line.ReturnedQty,
                        UnitPrice = line.UnitPrice,
                        RefundAmount = refundAmount,
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

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return new CreatePharmacyReturnResponseModel
                {
                    Success = true,
                    Message = "Return recorded.",
                    ReturnId = pharmacyReturn.ReturnId,
                    ReturnNo = returnNo,
                    TotalRefundAmount = totalRefund,
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                return new CreatePharmacyReturnResponseModel { Success = false, Message = $"Error recording return: {ex.Message}" };
            }
        }
    }
}
