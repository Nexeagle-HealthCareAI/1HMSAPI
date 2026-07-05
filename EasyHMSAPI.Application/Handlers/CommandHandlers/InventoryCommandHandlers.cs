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

                var scheduleClass = string.IsNullOrWhiteSpace(request.ScheduleClass) ? null : request.ScheduleClass.Trim().ToUpperInvariant();
                if (scheduleClass != null && !IpdConstants.DrugScheduleClass.All.Contains(scheduleClass))
                    return new CreateInventoryItemResponseModel { Success = false, Message = "Invalid drug schedule class." };

                var storageCondition = string.IsNullOrWhiteSpace(request.StorageCondition) ? null : request.StorageCondition.Trim().ToUpperInvariant();
                if (storageCondition != null && !IpdConstants.StorageCondition.All.Contains(storageCondition))
                    return new CreateInventoryItemResponseModel { Success = false, Message = "Invalid storage condition." };

                var now = DateTime.UtcNow;

                if (request.InventoryItemId.HasValue && request.InventoryItemId != Guid.Empty)
                {
                    var existingItem = await _context.InventoryItem
                        .FirstOrDefaultAsync(i => i.InventoryItemId == request.InventoryItemId && i.HospitalId == request.HospitalId, cancellationToken);
                    if (existingItem == null)
                        return new CreateInventoryItemResponseModel { Success = false, Message = "Inventory item not found." };

                    var codeTaken = await _context.InventoryItem.AnyAsync(
                        i => i.HospitalId == request.HospitalId && i.ItemCode == request.ItemCode.Trim() && i.InventoryItemId != existingItem.InventoryItemId, cancellationToken);
                    if (codeTaken)
                        return new CreateInventoryItemResponseModel { Success = false, Message = "An item with this code already exists." };

                    existingItem.ItemCode = request.ItemCode.Trim();
                    existingItem.ItemName = request.ItemName.Trim();
                    existingItem.GenericName = string.IsNullOrWhiteSpace(request.GenericName) ? null : request.GenericName.Trim();
                    existingItem.Manufacturer = string.IsNullOrWhiteSpace(request.Manufacturer) ? null : request.Manufacturer.Trim();
                    existingItem.Category = category;
                    existingItem.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "PCS" : request.Unit.Trim();
                    existingItem.DefaultRate = request.DefaultRate;
                    existingItem.HsnSacCode = request.HsnSacCode;
                    existingItem.GstSlabPercent = request.GstSlabPercent;
                    existingItem.IsTaxable = request.IsTaxable;
                    existingItem.ChargeId = request.ChargeId;
                    existingItem.MinStockLevel = request.MinStockLevel;
                    existingItem.StoreLocation = string.IsNullOrWhiteSpace(request.StoreLocation) ? null : request.StoreLocation.Trim();
                    existingItem.ScheduleClass = scheduleClass;
                    existingItem.IsLasa = request.IsLasa;
                    existingItem.IsHighAlert = request.IsHighAlert;
                    existingItem.StorageCondition = storageCondition;
                    existingItem.ReorderQty = request.ReorderQty;
                    existingItem.MaxStockLevel = request.MaxStockLevel;
                    existingItem.IsActive = request.IsActive;
                    existingItem.UpdatedAt = now;
                    existingItem.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);
                    return new CreateInventoryItemResponseModel { Success = true, Message = "Item updated.", InventoryItemId = existingItem.InventoryItemId };
                }

                var exists = await _context.InventoryItem.AnyAsync(
                    i => i.HospitalId == request.HospitalId && i.ItemCode == request.ItemCode.Trim(), cancellationToken);
                if (exists)
                    return new CreateInventoryItemResponseModel { Success = false, Message = "An item with this code already exists." };

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
                    ScheduleClass = scheduleClass,
                    IsLasa = request.IsLasa,
                    IsHighAlert = request.IsHighAlert,
                    StorageCondition = storageCondition,
                    ReorderQty = request.ReorderQty,
                    MaxStockLevel = request.MaxStockLevel,
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

                var isInbound = IpdConstants.InventoryMovementType.Inbound.Contains(movementType);
                var delta = isInbound ? request.Qty : -request.Qty;

                if (item.CurrentStock + delta < 0)
                    return new RecordInventoryMovementResponseModel { Success = false, Message = $"Insufficient stock — only {item.CurrentStock} {item.Unit} available." };

                // Batch/store-aware path (INV-2) — resolve which batch (if any) this movement posts
                // against, entirely optional so legacy callers (no BatchId/StoreId) behave exactly as
                // before.
                Batch? batch = null;
                if (request.BatchId.HasValue && request.BatchId != Guid.Empty)
                {
                    batch = await _context.Batch.FirstOrDefaultAsync(
                        b => b.BatchId == request.BatchId && b.HospitalId == request.HospitalId && b.InventoryItemId == item.InventoryItemId, cancellationToken);
                    if (batch == null)
                        return new RecordInventoryMovementResponseModel { Success = false, Message = "Batch not found." };
                    if (!isInbound && batch.RemainingQty + delta < 0)
                        return new RecordInventoryMovementResponseModel { Success = false, Message = $"Insufficient stock in batch {batch.BatchNumber} — only {batch.RemainingQty} {item.Unit} remaining." };
                }
                else if (!isInbound && request.StoreId.HasValue && request.StoreId != Guid.Empty)
                {
                    batch = await FefoBatchAllocationService.AllocateAsync(_context, request.HospitalId, item.InventoryItemId, request.StoreId.Value, request.Qty, cancellationToken);
                    if (batch == null)
                        return new RecordInventoryMovementResponseModel { Success = false, Message = "No active batch has enough remaining stock in that store to cover this quantity." };
                }

                var storeId = batch?.StoreId ?? request.StoreId;

                StockLevel? stockLevel = null;
                if (storeId.HasValue && storeId != Guid.Empty)
                {
                    stockLevel = await _context.StockLevel.FirstOrDefaultAsync(
                        sl => sl.InventoryItemId == item.InventoryItemId && sl.StoreId == storeId, cancellationToken);
                    var currentQty = stockLevel?.QtyOnHand ?? 0;
                    if (currentQty + delta < 0)
                        return new RecordInventoryMovementResponseModel { Success = false, Message = "Insufficient stock at that store." };
                }

                var now = DateTime.UtcNow;
                var movement = new InventoryMovement
                {
                    InventoryMovementId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    InventoryItemId = item.InventoryItemId,
                    MovementType = movementType,
                    Qty = request.Qty,
                    UnitCost = request.UnitCost,
                    BatchNumber = batch?.BatchNumber ?? (string.IsNullOrWhiteSpace(request.BatchNumber) ? null : request.BatchNumber.Trim()),
                    ExpiryDate = batch?.ExpiryDate ?? request.ExpiryDate,
                    BatchId = batch?.BatchId,
                    FromStoreId = !isInbound ? storeId : null,
                    ToStoreId = isInbound ? storeId : null,
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

                if (batch != null)
                {
                    batch.RemainingQty += delta;
                    batch.UpdatedAt = now;
                    batch.UpdatedBy = request.LoggedInUserName;
                    if (batch.RemainingQty == 0 && batch.Status == "ACTIVE")
                        batch.Status = "EXHAUSTED";
                }

                if (storeId.HasValue && storeId != Guid.Empty)
                {
                    if (stockLevel == null)
                    {
                        stockLevel = new StockLevel
                        {
                            StockLevelId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            InventoryItemId = item.InventoryItemId,
                            StoreId = storeId.Value,
                            QtyOnHand = 0,
                            UpdatedAt = now,
                        };
                        _context.StockLevel.Add(stockLevel);
                    }
                    stockLevel.QtyOnHand += delta;
                    stockLevel.UpdatedAt = now;
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordInventoryMovementResponseModel
                {
                    Success = true,
                    Message = "Movement recorded.",
                    InventoryMovementId = movement.InventoryMovementId,
                    NewCurrentStock = item.CurrentStock,
                    BatchId = batch?.BatchId,
                };
            }
            catch (Exception)
            {
                return new RecordInventoryMovementResponseModel { Success = false, Message = "Error recording inventory movement." };
            }
        }
    }
}
