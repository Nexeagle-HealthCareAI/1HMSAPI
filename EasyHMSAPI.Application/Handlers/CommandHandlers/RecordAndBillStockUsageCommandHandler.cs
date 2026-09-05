using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Board-level "use stock, bill the patient" quick action -- mirrors IntraOpCommandHandlers'
    // nested-send-within-one-transaction shape: RecordInventoryMovementRequestModel (ISSUE) first,
    // then AddChargeEventRequestModel only if the item resolves to a ChargeMaster link.
    public class RecordAndBillStockUsageCommandHandler : IRequestHandler<RecordAndBillStockUsageRequestModel, RecordAndBillStockUsageResponseModel>
    {
        private readonly IMediator _mediator;
        private readonly AppDbContext _context;
        private readonly ILogger<RecordAndBillStockUsageCommandHandler> _logger;

        public RecordAndBillStockUsageCommandHandler(IMediator mediator, AppDbContext context, ILogger<RecordAndBillStockUsageCommandHandler> logger)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
        }

        public async Task<RecordAndBillStockUsageResponseModel> Handle(RecordAndBillStockUsageRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.StoreId == Guid.Empty || request.InventoryItemId == Guid.Empty
                || request.EncounterId == Guid.Empty || string.IsNullOrWhiteSpace(request.PatientId))
            {
                return new RecordAndBillStockUsageResponseModel { Success = false, Message = "HospitalId, StoreId, InventoryItemId, EncounterId, and PatientId are required." };
            }

            if (request.Qty <= 0)
            {
                return new RecordAndBillStockUsageResponseModel { Success = false, Message = "Qty must be greater than zero." };
            }

            var item = await _context.InventoryItem
                .FirstOrDefaultAsync(i => i.InventoryItemId == request.InventoryItemId && i.HospitalId == request.HospitalId, cancellationToken);
            if (item == null)
            {
                return new RecordAndBillStockUsageResponseModel { Success = false, Message = "Inventory item not found." };
            }

            // A plain _context.Database.BeginTransactionAsync() here throws at runtime against a
            // real SQL Server connection ("SqlServerRetryingExecutionStrategy does not support
            // user-initiated transactions") — the retrying execution strategy must own the
            // transaction, same pattern TransferStockCommandHandler/QuickReceiveStockCommandHandler
            // already use. Never caught by the in-memory-provider unit tests since InMemory has no
            // execution strategy — only surfaced testing this live against the real dev database.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => TryHandleAsync(request, item, cancellationToken));
        }

        private async Task<RecordAndBillStockUsageResponseModel> TryHandleAsync(RecordAndBillStockUsageRequestModel request, InventoryItem item, CancellationToken cancellationToken)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                {
                    HospitalId = request.HospitalId,
                    InventoryItemId = request.InventoryItemId,
                    StoreId = request.StoreId,
                    MovementType = "ISSUE",
                    Qty = request.Qty,
                    EncounterId = request.EncounterId,
                    PatientId = request.PatientId,
                    SourceModule = "INVENTORY_BOARD",
                    Notes = request.Notes,
                    LoggedInUserId = request.LoggedInUserId,
                    LoggedInUserName = request.LoggedInUserName,
                }, cancellationToken);

                if (!movementResponse.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new RecordAndBillStockUsageResponseModel { Success = false, Message = movementResponse.Message ?? "Could not deduct stock for this item." };
                }

                if (!item.ChargeId.HasValue)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new RecordAndBillStockUsageResponseModel
                    {
                        Success = true,
                        Message = "Usage recorded. This item has no charge configured, so nothing was billed.",
                        InventoryMovementId = movementResponse.InventoryMovementId,
                        InventoryMovementIds = movementResponse.InventoryMovementIds,
                        NoChargeConfigured = true,
                    };
                }

                var chargeResponse = await _mediator.Send(new AddChargeEventRequestModel
                {
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId,
                    EncounterId = request.EncounterId,
                    Charges = new List<ChargeDetail>
                    {
                        new ChargeDetail
                        {
                            ChargeId = item.ChargeId,
                            DisplayName = item.ItemName,
                            Qty = request.Qty,
                            Rate = item.DefaultRate ?? 0,
                            DiscountPercent = 0,
                            CategoryCode = item.Category,
                            AttributedDoctorId = request.AttributedDoctorId,
                        },
                    },
                    LoggedInUserName = request.LoggedInUserName,
                    LoggedInUserId = request.LoggedInUserId,
                }, cancellationToken);

                if (chargeResponse.Success != true || chargeResponse.Data?.ChargeEvents == null || chargeResponse.Data.ChargeEvents.Count == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new RecordAndBillStockUsageResponseModel { Success = false, Message = chargeResponse.Message ?? "Could not post the charge for this item." };
                }

                await transaction.CommitAsync(cancellationToken);
                return new RecordAndBillStockUsageResponseModel
                {
                    Success = true,
                    Message = "Usage recorded and billed.",
                    InventoryMovementId = movementResponse.InventoryMovementId,
                    InventoryMovementIds = movementResponse.InventoryMovementIds,
                    ChargeEventId = chargeResponse.Data.ChargeEvents[0].ChargeEventId,
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error recording and billing stock usage.");
                return new RecordAndBillStockUsageResponseModel { Success = false, Message = "An error occurred while recording and billing stock usage." };
            }
        }
    }
}
