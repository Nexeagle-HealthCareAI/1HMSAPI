using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
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

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Step 1: ISSUE from source store
                var issueResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                {
                    HospitalId = request.HospitalId,
                    InventoryItemId = request.InventoryItemId,
                    StoreId = request.FromStoreId,
                    BatchId = request.BatchId,
                    MovementType = "ISSUE",
                    Qty = request.Qty,
                    Reason = "TRANSFER_OUT",
                    Notes = $"Transfer to store {request.ToStoreId}. {request.Notes}",
                    LoggedInUserId = request.LoggedInUserId,
                    LoggedInUserName = request.LoggedInUserName
                }, cancellationToken);

                if (!issueResponse.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new TransferStockResponseModel { Success = false, Message = $"Failed to issue from source store: {issueResponse.Message}" };
                }

                // Step 2: RECEIVE to destination store — shares the same scoped AppDbContext as
                // step 1 (nested _mediator.Send resolves handlers from the same DI scope), so both
                // SaveChangesAsync calls enlist in the transaction opened above.
                var receiveResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                {
                    HospitalId = request.HospitalId,
                    InventoryItemId = request.InventoryItemId,
                    StoreId = request.ToStoreId,
                    BatchId = request.BatchId, // Batch travels with the item
                    MovementType = "RECEIVE",
                    Qty = request.Qty,
                    Reason = "TRANSFER_IN",
                    Notes = $"Transfer from store {request.FromStoreId}. {request.Notes}",
                    LoggedInUserId = request.LoggedInUserId,
                    LoggedInUserName = request.LoggedInUserName
                }, cancellationToken);

                if (!receiveResponse.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new TransferStockResponseModel { Success = false, Message = $"Failed to receive in destination store: {receiveResponse.Message}. Transfer was rolled back." };
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
