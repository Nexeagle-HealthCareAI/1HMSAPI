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
    // Return-to-vendor: deducts the returned batches for real via the shared movement handler
    // (MovementType=ADJUST_OUT, IsVendorReturnContext=true so near-expiry/expired batches — the
    // whole point of an RTV — aren't rejected by the usual "can't issue from an expired batch"
    // guard), then records a debit note (VendorReturnNote + lines) for the vendor ledger.
    public class CreateVendorReturnHandler : IRequestHandler<CreateVendorReturnRequestModel, CreateVendorReturnResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public CreateVendorReturnHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<CreateVendorReturnResponseModel> Handle(CreateVendorReturnRequestModel request, CancellationToken cancellationToken)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => TryHandleAsync(request, cancellationToken));
        }

        private async Task<CreateVendorReturnResponseModel> TryHandleAsync(CreateVendorReturnRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.VendorId == Guid.Empty)
                return new CreateVendorReturnResponseModel { Success = false, Message = "HospitalId and VendorId are required." };
            if (request.Lines == null || request.Lines.Count == 0)
                return new CreateVendorReturnResponseModel { Success = false, Message = "At least one return line is required." };
            if (request.Lines.Any(l => l.Qty <= 0))
                return new CreateVendorReturnResponseModel { Success = false, Message = "Return quantity must be greater than zero on every line." };

            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var vendor = await _context.Vendor.FirstOrDefaultAsync(
                    v => v.VendorId == request.VendorId && v.HospitalId == request.HospitalId, cancellationToken);
                if (vendor == null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new CreateVendorReturnResponseModel { Success = false, Message = "Vendor not found." };
                }

                var batchIds = request.Lines.Select(l => l.BatchId).Distinct().ToList();
                var batches = await _context.Batch.Where(b => batchIds.Contains(b.BatchId)).ToDictionaryAsync(b => b.BatchId, cancellationToken);

                var now = DateTime.UtcNow;
                decimal totalQty = 0;
                decimal totalValue = 0;
                var returnLines = new List<VendorReturnLine>();

                foreach (var line in request.Lines)
                {
                    if (!batches.TryGetValue(line.BatchId, out var batch) || batch.HospitalId != request.HospitalId || batch.VendorId != request.VendorId)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new CreateVendorReturnResponseModel { Success = false, Message = "One or more batches were not found for this vendor." };
                    }
                    if (line.Qty > batch.RemainingQty)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new CreateVendorReturnResponseModel
                        {
                            Success = false,
                            Message = $"Cannot return {line.Qty} from batch {batch.BatchNumber} — only {batch.RemainingQty} remaining."
                        };
                    }

                    var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                    {
                        HospitalId = request.HospitalId,
                        InventoryItemId = batch.InventoryItemId,
                        BatchId = batch.BatchId,
                        MovementType = IpdConstants.InventoryMovementType.AdjustOut,
                        Qty = line.Qty,
                        SourceModule = "PHARMACY_RTV",
                        Reason = "RETURN_TO_VENDOR",
                        Notes = request.Notes,
                        IsVendorReturnContext = true,
                        LoggedInUserId = request.LoggedInUserId,
                        LoggedInUserName = request.LoggedInUserName,
                    }, cancellationToken);

                    if (!movementResponse.Success)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new CreateVendorReturnResponseModel { Success = false, Message = $"Stock deduction failed: {movementResponse.Message}" };
                    }

                    var unitCost = batch.UnitCost ?? 0;
                    var lineValue = Math.Round(unitCost * line.Qty, 2);
                    totalQty += line.Qty;
                    totalValue += lineValue;

                    returnLines.Add(new VendorReturnLine
                    {
                        VendorReturnLineId = Guid.NewGuid(),
                        InventoryItemId = batch.InventoryItemId,
                        BatchId = batch.BatchId,
                        BatchNumber = batch.BatchNumber,
                        ExpiryDate = batch.ExpiryDate,
                        Qty = line.Qty,
                        UnitCost = unitCost,
                        LineValue = lineValue,
                    });
                }

                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.VendorReturn, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var returnNoteNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);

                var vendorReturn = new VendorReturnNote
                {
                    VendorReturnId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    VendorId = request.VendorId,
                    ReturnNoteNo = returnNoteNo,
                    TotalQty = totalQty,
                    TotalValue = totalValue,
                    Notes = request.Notes,
                    GeneratedAt = now,
                    GeneratedBy = request.LoggedInUserName,
                    GeneratedByUserId = request.LoggedInUserId,
                    CreatedAt = now,
                };
                _context.VendorReturnNote.Add(vendorReturn);
                // Saved before the lines: VendorReturnLine.VendorReturnId is a plain FK column, not
                // an EF navigation property, so nothing tells SaveChangesAsync to insert the parent
                // first — without this, real SQL Server intermittently inserts a line before its
                // note and trips FK_RTVL_Return (same class of bug as the GRN/Batch ordering fix).
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var rl in returnLines)
                {
                    rl.VendorReturnId = vendorReturn.VendorReturnId;
                    _context.VendorReturnLine.Add(rl);
                }

                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return new CreateVendorReturnResponseModel
                {
                    Success = true,
                    Message = "Vendor return note generated.",
                    VendorReturnId = vendorReturn.VendorReturnId,
                    ReturnNoteNo = returnNoteNo,
                    TotalQty = totalQty,
                    TotalValue = totalValue,
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                return new CreateVendorReturnResponseModel { Success = false, Message = $"Error generating vendor return: {ex.Message}" };
            }
        }
    }
}
