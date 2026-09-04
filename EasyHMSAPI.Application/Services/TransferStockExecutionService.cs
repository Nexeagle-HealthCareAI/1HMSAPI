using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using MediatR;

namespace EasyHMSAPI.Application.Services
{
    // Shared "move stock between two stores" core (ISSUE from source + RECEIVE into destination
    // per batch allocation), extracted out of TransferStockCommandHandler so a caller that needs
    // the transfer as one step of a LARGER atomic operation (e.g. IssueIndent, which must also
    // update Indent/IndentLine status in the SAME transaction) can invoke it directly instead of
    // going through TransferStockCommandHandler.Handle() - which opens and commits its OWN
    // transaction on the shared DbContext, and EF Core does not support starting a second
    // BeginTransactionAsync while one is already active on that context.
    public static class TransferStockExecutionService
    {
        public static async Task<(bool Success, string? Message)> ExecuteAsync(
            IMediator mediator,
            Guid hospitalId,
            Guid inventoryItemId,
            Guid fromStoreId,
            Guid toStoreId,
            List<BatchAllocation> allocations,
            string? notes,
            string? loggedInUserName,
            Guid? loggedInUserId,
            CancellationToken cancellationToken)
        {
            foreach (var alloc in allocations)
            {
                var issueResponse = await mediator.Send(new RecordInventoryMovementRequestModel
                {
                    HospitalId = hospitalId,
                    InventoryItemId = inventoryItemId,
                    StoreId = fromStoreId,
                    BatchId = alloc.Batch.BatchId,
                    MovementType = "ISSUE",
                    Qty = alloc.AllocatedQty,
                    Reason = "TRANSFER_OUT",
                    Notes = $"Transfer to store {toStoreId}. {notes}",
                    LoggedInUserId = loggedInUserId,
                    LoggedInUserName = loggedInUserName
                }, cancellationToken);

                if (!issueResponse.Success)
                    return (false, $"Failed to issue batch {alloc.Batch.BatchNumber} from source store: {issueResponse.Message}");

                var receiveResponse = await mediator.Send(new RecordInventoryMovementRequestModel
                {
                    HospitalId = hospitalId,
                    InventoryItemId = inventoryItemId,
                    StoreId = toStoreId,
                    BatchId = alloc.Batch.BatchId,
                    MovementType = "RECEIVE",
                    Qty = alloc.AllocatedQty,
                    Reason = "TRANSFER_IN",
                    Notes = $"Transfer from store {fromStoreId}. {notes}",
                    LoggedInUserId = loggedInUserId,
                    LoggedInUserName = loggedInUserName
                }, cancellationToken);

                if (!receiveResponse.Success)
                    return (false, $"Failed to receive batch {alloc.Batch.BatchNumber} in destination store: {receiveResponse.Message}. Transfer was rolled back.");
            }

            return (true, "Stock transferred successfully.");
        }
    }
}
