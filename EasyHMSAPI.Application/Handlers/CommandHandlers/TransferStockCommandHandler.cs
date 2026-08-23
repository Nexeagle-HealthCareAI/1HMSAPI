using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class TransferStockCommandHandler : IRequestHandler<TransferStockRequestModel, TransferStockResponseModel>
    {
        private readonly IMediator _mediator;
        private readonly AppDbContext _context;
        private readonly ILogger<TransferStockCommandHandler> _logger;

        public TransferStockCommandHandler(IMediator mediator, AppDbContext context, ILogger<TransferStockCommandHandler> logger)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
        }

        public async Task<TransferStockResponseModel> Handle(TransferStockRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.InventoryItemId == Guid.Empty || 
                request.FromStoreId == Guid.Empty || request.ToStoreId == Guid.Empty)
            {
                return new TransferStockResponseModel { Success = false, Message = "Missing required parameters." };
            }

            if (request.FromStoreId == request.ToStoreId)
            {
                return new TransferStockResponseModel { Success = false, Message = "Source and destination stores cannot be the same." };
            }

            if (request.Qty <= 0)
            {
                return new TransferStockResponseModel { Success = false, Message = "Transfer quantity must be greater than zero." };
            }

            // Figure out batch allocations upfront so we can explicitly pass them to ISSUE and RECEIVE
            var allocations = new List<BatchAllocation>();
            if (request.BatchId.HasValue && request.BatchId != Guid.Empty)
            {
                var singleBatch = await _context.Batch.FirstOrDefaultAsync(
                    b => b.BatchId == request.BatchId && b.HospitalId == request.HospitalId && b.InventoryItemId == request.InventoryItemId, cancellationToken);
                
                if (singleBatch == null)
                    return new TransferStockResponseModel { Success = false, Message = "Requested batch not found." };
                
                allocations.Add(new BatchAllocation { Batch = singleBatch, AllocatedQty = request.Qty });
            }
            else
            {
                allocations = await FefoBatchAllocationService.AllocateAsync(_context, request.HospitalId, request.InventoryItemId, request.FromStoreId, request.Qty, cancellationToken);
                if (allocations == null || allocations.Count == 0)
                    return new TransferStockResponseModel { Success = false, Message = "No active batch has enough remaining stock in the source store to cover this transfer quantity." };
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var alloc in allocations)
                {
                    // Step 1: ISSUE from source store
                    var issueResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                    {
                        HospitalId = request.HospitalId,
                        InventoryItemId = request.InventoryItemId,
                        StoreId = request.FromStoreId,
                        BatchId = alloc.Batch.BatchId,
                        MovementType = "ISSUE",
                        Qty = alloc.AllocatedQty,
                        Reason = "TRANSFER_OUT",
                        Notes = $"Transfer to store {request.ToStoreId}. {request.Notes}",
                        LoggedInUserId = request.LoggedInUserId,
                        LoggedInUserName = request.LoggedInUserName
                    }, cancellationToken);

                    if (!issueResponse.Success)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new TransferStockResponseModel { Success = false, Message = $"Failed to issue batch {alloc.Batch.BatchNumber} from source store: {issueResponse.Message}" };
                    }

                    // Step 2: RECEIVE to destination store
                    // The InventoryCommandHandler now automatically clones the batch for the destination store
                    var receiveResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                    {
                        HospitalId = request.HospitalId,
                        InventoryItemId = request.InventoryItemId,
                        StoreId = request.ToStoreId,
                        BatchId = alloc.Batch.BatchId,
                        MovementType = "RECEIVE",
                        Qty = alloc.AllocatedQty,
                        Reason = "TRANSFER_IN",
                        Notes = $"Transfer from store {request.FromStoreId}. {request.Notes}",
                        LoggedInUserId = request.LoggedInUserId,
                        LoggedInUserName = request.LoggedInUserName
                    }, cancellationToken);

                    if (!receiveResponse.Success)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new TransferStockResponseModel { Success = false, Message = $"Failed to receive batch {alloc.Batch.BatchNumber} in destination store: {receiveResponse.Message}. Transfer was rolled back." };
                    }
                }

                await transaction.CommitAsync(cancellationToken);
                return new TransferStockResponseModel { Success = true, Message = "Stock transferred successfully." };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error executing transfer.");
                return new TransferStockResponseModel { Success = false, Message = "An error occurred during the transfer." };
            }
        }
    }
}
