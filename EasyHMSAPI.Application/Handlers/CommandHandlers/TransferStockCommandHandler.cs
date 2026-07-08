using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class TransferStockCommandHandler : IRequestHandler<TransferStockRequestModel, TransferStockResponseModel>
    {
        private readonly IMediator _mediator;
        private readonly ILogger<TransferStockCommandHandler> _logger;

        public TransferStockCommandHandler(IMediator mediator, ILogger<TransferStockCommandHandler> logger)
        {
            _mediator = mediator;
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
                    return new TransferStockResponseModel { Success = false, Message = $"Failed to issue from source store: {issueResponse.Message}" };
                }

                // Step 2: RECEIVE to destination store
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
                    // This is a partial failure state! Ideally this should be wrapped in a distributed transaction 
                    // or a single EF Core transaction if they shared a context, but RecordMovement saves its own context.
                    // For now, this is a known edge case that requires manual adjustment if it occurs.
                    _logger.LogCritical("Transfer partial failure: Issued {Qty} of Item {Item} from {FromStore} but failed to receive in {ToStore}. Reason: {Msg}",
                        request.Qty, request.InventoryItemId, request.FromStoreId, request.ToStoreId, receiveResponse.Message);
                        
                    return new TransferStockResponseModel { Success = false, Message = $"Failed to receive in destination store. Source stock was deducted. Contact admin." };
                }

                return new TransferStockResponseModel { Success = true, Message = "Stock transferred successfully." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing transfer.");
                return new TransferStockResponseModel { Success = false, Message = "An error occurred during the transfer." };
            }
        }
    }
}
