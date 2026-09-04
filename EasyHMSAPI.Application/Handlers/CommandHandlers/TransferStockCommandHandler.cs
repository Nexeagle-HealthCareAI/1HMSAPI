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

            // A plain _context.Database.BeginTransactionAsync() here throws at runtime against a
            // real SQL Server connection ("SqlServerRetryingExecutionStrategy does not support
            // user-initiated transactions") — the retrying execution strategy must own the
            // transaction, same pattern BulkBatchCommandHandlers/PharmacyRetailCheckoutCommandHandler
            // already use. Never caught by the in-memory-provider unit tests since InMemory has no
            // execution strategy — only surfaced testing this live against the real dev database.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => TryHandleAsync(request, allocations, cancellationToken));
        }

        private async Task<TransferStockResponseModel> TryHandleAsync(TransferStockRequestModel request, List<BatchAllocation> allocations, CancellationToken cancellationToken)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var (success, message) = await TransferStockExecutionService.ExecuteAsync(
                    _mediator, request.HospitalId, request.InventoryItemId, request.FromStoreId, request.ToStoreId,
                    allocations, request.Notes, request.LoggedInUserName, request.LoggedInUserId, cancellationToken);

                if (!success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new TransferStockResponseModel { Success = false, Message = message };
                }

                await transaction.CommitAsync(cancellationToken);
                return new TransferStockResponseModel { Success = true, Message = message };
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
