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
    // Per line: creates a Batch (ReceivedQty=Qty, RemainingQty=0), then nested-sends
    // RecordInventoryMovementRequestModel (RECEIVE) to bring RemainingQty/StockLevel/CurrentStock
    // up via the SAME handler every other movement uses — no duplicated stock-mutation logic.
    // Wrapped in an explicit transaction, same pattern as IntraOpCommandHandlers.RecordIntraOpItemUsage:
    // any line failing rolls back the whole GRN.
    public class GoodsReceiptNoteCommandHandlers : IRequestHandler<CreateGoodsReceiptNoteRequestModel, CreateGoodsReceiptNoteResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public GoodsReceiptNoteCommandHandlers(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<CreateGoodsReceiptNoteResponseModel> Handle(CreateGoodsReceiptNoteRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.PurchaseOrderId == Guid.Empty || request.ReceivedStoreId == Guid.Empty)
                    return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = "HospitalId, PurchaseOrderId, and ReceivedStoreId are required." };
                if (request.Lines.Count == 0)
                    return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = "At least one line is required." };
                if (request.Lines.Any(l => l.Qty <= 0 || string.IsNullOrWhiteSpace(l.BatchNumber)))
                    return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = "Every line needs a positive quantity and a batch number." };

                var po = await _context.PurchaseOrder.FirstOrDefaultAsync(
                    p => p.PurchaseOrderId == request.PurchaseOrderId && p.HospitalId == request.HospitalId, cancellationToken);
                if (po == null)
                    return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = "Purchase order not found." };

                var receivable = new[] { IpdConstants.PurchaseOrderStatus.Approved, IpdConstants.PurchaseOrderStatus.Sent, IpdConstants.PurchaseOrderStatus.PartiallyReceived };
                if (!receivable.Contains(po.Status))
                    return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = $"Purchase order is {po.Status.ToLowerInvariant()} and cannot be received against." };

                var storeExists = await _context.Store.AnyAsync(
                    s => s.StoreId == request.ReceivedStoreId && s.HospitalId == request.HospitalId, cancellationToken);
                if (!storeExists)
                    return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = "Receiving store not found." };

                var poLines = await _context.PurchaseOrderLine.Where(l => l.PurchaseOrderId == po.PurchaseOrderId).ToListAsync(cancellationToken);
                var poLinesById = poLines.ToDictionary(l => l.PurchaseOrderLineId);

                foreach (var line in request.Lines)
                {
                    if (!poLinesById.TryGetValue(line.PurchaseOrderLineId, out var poLine))
                        return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = "One or more lines do not belong to this purchase order." };
                    if (poLine.InventoryItemId != line.InventoryItemId)
                        return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = "Line item does not match the purchase order line." };
                    var remaining = poLine.Qty - poLine.ReceivedQty;
                    if (line.Qty > remaining)
                        return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = $"Cannot receive {line.Qty} — only {remaining} remaining on that PO line." };
                }

                var lineTotal = request.Lines.Sum(l => l.Qty * l.Rate);
                var matchStatus = request.InvoiceAmount == null
                    ? IpdConstants.GrnMatchStatus.Pending
                    : Math.Abs(request.InvoiceAmount.Value - lineTotal) < 0.01m
                        ? IpdConstants.GrnMatchStatus.Matched
                        : IpdConstants.GrnMatchStatus.Mismatch;

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var now = DateTime.UtcNow;
                        var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                            _context, request.HospitalId, BillingConstants.NumberSeriesCode.Grn, request.LoggedInUserName, cancellationToken);
                        numberSeries.CurrentValue++;
                        var grnNumber = NumberSeriesFormatter.Format(
                            numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);

                        var grn = new GoodsReceiptNote
                        {
                            GrnId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            GrnNumber = grnNumber,
                            PurchaseOrderId = po.PurchaseOrderId,
                            VendorId = po.VendorId,
                            ReceivedStoreId = request.ReceivedStoreId,
                            InvoiceNumber = request.InvoiceNumber,
                            InvoiceDate = request.InvoiceDate,
                            InvoiceAmount = request.InvoiceAmount,
                            MatchStatus = matchStatus,
                            ReceivedBy = request.LoggedInUserName,
                            ReceivedByUserId = request.LoggedInUserId,
                            ReceivedAt = now,
                            Notes = request.Notes,
                            CreatedAt = now,
                            CreatedBy = request.LoggedInUserName,
                        };
                        _context.GoodsReceiptNote.Add(grn);

                        foreach (var line in request.Lines)
                        {
                            var freeQty = line.FreeQty > 0 ? line.FreeQty : 0m;
                            var totalReceivedQty = line.Qty + freeQty;
                            // Landing cost spread over every physical unit received, billed or free —
                            // "100 billed + 10 free @ Rs.10" lands at Rs.9.09/unit, not Rs.10.
                            var effectiveUnitCost = totalReceivedQty > 0 ? Math.Round((line.Qty * line.Rate) / totalReceivedQty, 4) : line.Rate;

                            var grnLine = new GoodsReceiptNoteLine
                            {
                                GrnLineId = Guid.NewGuid(),
                                GrnId = grn.GrnId,
                                PurchaseOrderLineId = line.PurchaseOrderLineId,
                                InventoryItemId = line.InventoryItemId,
                                BatchNumber = line.BatchNumber.Trim(),
                                ManufactureDate = line.ManufactureDate,
                                ExpiryDate = line.ExpiryDate,
                                Qty = line.Qty,
                                FreeQty = freeQty,
                                Rate = line.Rate,
                            };
                            _context.GoodsReceiptNoteLine.Add(grnLine);

                            var batch = new Batch
                            {
                                BatchId = Guid.NewGuid(),
                                HospitalId = request.HospitalId,
                                InventoryItemId = line.InventoryItemId,
                                StoreId = request.ReceivedStoreId,
                                BatchNumber = line.BatchNumber.Trim(),
                                ManufactureDate = line.ManufactureDate,
                                ExpiryDate = line.ExpiryDate,
                                UnitCost = effectiveUnitCost,
                                ReceivedQty = totalReceivedQty,
                                RemainingQty = 0,
                                VendorId = po.VendorId,
                                GrnLineId = grnLine.GrnLineId,
                                Status = "ACTIVE",
                                CreatedAt = now,
                                CreatedBy = request.LoggedInUserName,
                                UpdatedAt = now,
                                UpdatedBy = request.LoggedInUserName,
                            };
                            _context.Batch.Add(batch);

                            // Save changes so that the RecordInventoryMovementHandler can query this newly created batch from the DB (within the transaction).
                            await _context.SaveChangesAsync(cancellationToken);

                            var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                            {
                                HospitalId = request.HospitalId,
                                InventoryItemId = line.InventoryItemId,
                                MovementType = IpdConstants.InventoryMovementType.Receive,
                                Qty = totalReceivedQty,
                                UnitCost = effectiveUnitCost,
                                BatchId = batch.BatchId,
                                StoreId = request.ReceivedStoreId,
                                SourceModule = "PROCUREMENT",
                                SourceRefId = grn.GrnId.ToString(),
                                LoggedInUserName = request.LoggedInUserName,
                                LoggedInUserId = request.LoggedInUserId,
                            }, cancellationToken);

                            if (!movementResponse.Success)
                            {
                                await tx.RollbackAsync(cancellationToken);
                                return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = movementResponse.Message ?? "Could not post the receipt for this line." };
                            }

                            var poLine = poLinesById[line.PurchaseOrderLineId];
                            poLine.ReceivedQty += line.Qty;
                        }

                        po.Status = poLines.All(l => l.ReceivedQty >= l.Qty)
                            ? IpdConstants.PurchaseOrderStatus.Received
                            : IpdConstants.PurchaseOrderStatus.PartiallyReceived;
                        po.UpdatedAt = now;
                        po.UpdatedBy = request.LoggedInUserName;

                        await _context.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);

                        return new CreateGoodsReceiptNoteResponseModel
                        {
                            Success = true,
                            Message = "Goods receipt recorded.",
                            GrnId = grn.GrnId,
                            GrnNumber = grn.GrnNumber,
                            MatchStatus = grn.MatchStatus,
                        };
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                        return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = $"Error recording goods receipt: {errorMsg}" };
                    }
                });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return new CreateGoodsReceiptNoteResponseModel { Success = false, Message = $"Error recording goods receipt: {errorMsg}" };
            }
        }
    }
}
