using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Board-level "receive stock" quick action — composes CreateBatchRequestModel and
    // RecordInventoryMovementRequestModel (the two calls a caller would otherwise have to make
    // separately) inside one DB transaction, following the same nested-_mediator.Send-within-a-
    // transaction pattern as TransferStockCommandHandler.
    public class QuickReceiveStockCommandHandler : IRequestHandler<QuickReceiveStockRequestModel, QuickReceiveStockResponseModel>
    {
        private readonly IMediator _mediator;
        private readonly AppDbContext _context;
        private readonly ILogger<QuickReceiveStockCommandHandler> _logger;

        public QuickReceiveStockCommandHandler(IMediator mediator, AppDbContext context, ILogger<QuickReceiveStockCommandHandler> logger)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
        }

        public async Task<QuickReceiveStockResponseModel> Handle(QuickReceiveStockRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.StoreId == Guid.Empty || request.InventoryItemId == Guid.Empty)
            {
                return new QuickReceiveStockResponseModel { Success = false, Message = "HospitalId, StoreId, and InventoryItemId are required." };
            }

            if (request.Qty <= 0)
            {
                return new QuickReceiveStockResponseModel { Success = false, Message = "Qty must be greater than zero." };
            }

            // A plain _context.Database.BeginTransactionAsync() here throws at runtime against a
            // real SQL Server connection ("SqlServerRetryingExecutionStrategy does not support
            // user-initiated transactions") — the retrying execution strategy must own the
            // transaction, same pattern BulkBatchCommandHandlers/PharmacyRetailCheckoutCommandHandler
            // already use. Never caught by the in-memory-provider unit tests since InMemory has no
            // execution strategy — only surfaced testing this live against the real dev database.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => TryHandleAsync(request, cancellationToken));
        }

        private async Task<QuickReceiveStockResponseModel> TryHandleAsync(QuickReceiveStockRequestModel request, CancellationToken cancellationToken)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var batchNumber = string.IsNullOrWhiteSpace(request.BatchNumber)
                    ? $"ADHOC-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                    : request.BatchNumber.Trim();

                var batchResponse = await _mediator.Send(new CreateBatchRequestModel
                {
                    HospitalId = request.HospitalId,
                    InventoryItemId = request.InventoryItemId,
                    StoreId = request.StoreId,
                    BatchNumber = batchNumber,
                    ManufactureDate = request.ManufactureDate,
                    ExpiryDate = request.ExpiryDate,
                    UnitCost = request.UnitCost,
                    ReceivedQty = request.Qty,
                    LoggedInUserName = request.LoggedInUserName,
                }, cancellationToken);

                if (!batchResponse.Success || !batchResponse.BatchId.HasValue)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new QuickReceiveStockResponseModel { Success = false, Message = $"Failed to create batch: {batchResponse.Message}" };
                }

                var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                {
                    HospitalId = request.HospitalId,
                    InventoryItemId = request.InventoryItemId,
                    StoreId = request.StoreId,
                    BatchId = batchResponse.BatchId,
                    MovementType = "RECEIVE",
                    Qty = request.Qty,
                    UnitCost = request.UnitCost,
                    Reason = "QUICK_RECEIVE",
                    Notes = request.Notes,
                    LoggedInUserId = request.LoggedInUserId,
                    LoggedInUserName = request.LoggedInUserName,
                }, cancellationToken);

                if (!movementResponse.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new QuickReceiveStockResponseModel { Success = false, Message = $"Failed to record receipt: {movementResponse.Message}" };
                }

                await transaction.CommitAsync(cancellationToken);
                return new QuickReceiveStockResponseModel
                {
                    Success = true,
                    Message = "Stock received.",
                    BatchId = batchResponse.BatchId,
                    InventoryMovementId = movementResponse.InventoryMovementId,
                    NewCurrentStock = movementResponse.NewCurrentStock,
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error executing quick receive.");
                return new QuickReceiveStockResponseModel { Success = false, Message = "An error occurred while receiving stock." };
            }
        }
    }
}
