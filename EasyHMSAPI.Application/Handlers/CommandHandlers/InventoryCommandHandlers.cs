using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Wires the dead-scaffolded InventoryItem/InventoryMovement schema. CurrentStock is not
    /// trigger-maintained — RecordInventoryMovementRequestModel is the single place that both
    /// inserts a movement row and adjusts InventoryItem.CurrentStock, so every caller (this
    /// controller, or a nested _mediator.Send() from IntraOpItemUsage/CSSD handlers) gets the
    /// same guarantee.
    /// </summary>
    public class InventoryCommandHandlers :
        IRequestHandler<CreateInventoryItemRequestModel, CreateInventoryItemResponseModel>,
        IRequestHandler<RecordInventoryMovementRequestModel, RecordInventoryMovementResponseModel>
    {
        private readonly AppDbContext _context;

        public InventoryCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateInventoryItemResponseModel> Handle(CreateInventoryItemRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.ItemCode) || string.IsNullOrWhiteSpace(request.ItemName))
                    return new CreateInventoryItemResponseModel { Success = false, Message = "HospitalId, ItemCode, and ItemName are required." };

                var category = request.Category?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(category) || !IpdConstants.InventoryCategory.All.Contains(category))
                    return new CreateInventoryItemResponseModel { Success = false, Message = "Invalid category." };

                var exists = await _context.InventoryItem.AnyAsync(
                    i => i.HospitalId == request.HospitalId && i.ItemCode == request.ItemCode.Trim(), cancellationToken);
                if (exists)
                    return new CreateInventoryItemResponseModel { Success = false, Message = "An item with this code already exists." };

                var now = DateTime.UtcNow;
                var item = new InventoryItem
                {
                    InventoryItemId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    ItemCode = request.ItemCode.Trim(),
                    ItemName = request.ItemName.Trim(),
                    GenericName = string.IsNullOrWhiteSpace(request.GenericName) ? null : request.GenericName.Trim(),
                    Manufacturer = string.IsNullOrWhiteSpace(request.Manufacturer) ? null : request.Manufacturer.Trim(),
                    Category = category,
                    Unit = string.IsNullOrWhiteSpace(request.Unit) ? "PCS" : request.Unit.Trim(),
                    DefaultRate = request.DefaultRate,
                    HsnSacCode = request.HsnSacCode,
                    GstSlabPercent = request.GstSlabPercent,
                    IsTaxable = request.IsTaxable,
                    ChargeId = request.ChargeId,
                    CurrentStock = 0,
                    MinStockLevel = request.MinStockLevel,
                    StoreLocation = string.IsNullOrWhiteSpace(request.StoreLocation) ? null : request.StoreLocation.Trim(),
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.InventoryItem.Add(item);
                await _context.SaveChangesAsync(cancellationToken);

                return new CreateInventoryItemResponseModel { Success = true, Message = "Item created.", InventoryItemId = item.InventoryItemId };
            }
            catch (Exception)
            {
                return new CreateInventoryItemResponseModel { Success = false, Message = "Error creating inventory item." };
            }
        }

        public async Task<RecordInventoryMovementResponseModel> Handle(RecordInventoryMovementRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.InventoryItemId == Guid.Empty)
                    return new RecordInventoryMovementResponseModel { Success = false, Message = "HospitalId and InventoryItemId are required." };

                var movementType = request.MovementType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(movementType) || !IpdConstants.InventoryMovementType.All.Contains(movementType))
                    return new RecordInventoryMovementResponseModel { Success = false, Message = "Invalid movement type." };

                if (request.Qty <= 0)
                    return new RecordInventoryMovementResponseModel { Success = false, Message = "Qty must be greater than zero." };

                var item = await _context.InventoryItem
                    .FirstOrDefaultAsync(i => i.InventoryItemId == request.InventoryItemId && i.HospitalId == request.HospitalId, cancellationToken);
                if (item == null)
                    return new RecordInventoryMovementResponseModel { Success = false, Message = "Inventory item not found." };

                var isInbound = movementType == IpdConstants.InventoryMovementType.Receive
                    || movementType == IpdConstants.InventoryMovementType.Return
                    || movementType == IpdConstants.InventoryMovementType.AdjustIn;
                var delta = isInbound ? request.Qty : -request.Qty;

                if (item.CurrentStock + delta < 0)
                    return new RecordInventoryMovementResponseModel { Success = false, Message = $"Insufficient stock — only {item.CurrentStock} {item.Unit} available." };

                var now = DateTime.UtcNow;
                var movement = new InventoryMovement
                {
                    InventoryMovementId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    InventoryItemId = item.InventoryItemId,
                    MovementType = movementType,
                    Qty = request.Qty,
                    UnitCost = request.UnitCost,
                    BatchNumber = string.IsNullOrWhiteSpace(request.BatchNumber) ? null : request.BatchNumber.Trim(),
                    ExpiryDate = request.ExpiryDate,
                    EncounterId = request.EncounterId,
                    PatientId = request.PatientId,
                    ChargeEventId = request.ChargeEventId,
                    SourceModule = string.IsNullOrWhiteSpace(request.SourceModule) ? null : request.SourceModule.Trim(),
                    SourceRefId = string.IsNullOrWhiteSpace(request.SourceRefId) ? null : request.SourceRefId.Trim(),
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    MovedAt = now,
                    MovedBy = request.LoggedInUserName,
                    MovedByUserId = request.LoggedInUserId,
                    CreatedAt = now,
                };
                _context.InventoryMovement.Add(movement);

                item.CurrentStock += delta;
                item.UpdatedAt = now;
                item.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordInventoryMovementResponseModel
                {
                    Success = true,
                    Message = "Movement recorded.",
                    InventoryMovementId = movement.InventoryMovementId,
                    NewCurrentStock = item.CurrentStock,
                };
            }
            catch (Exception)
            {
                return new RecordInventoryMovementResponseModel { Success = false, Message = "Error recording inventory movement." };
            }
        }
    }
}
