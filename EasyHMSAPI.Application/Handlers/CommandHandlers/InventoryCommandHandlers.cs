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
                    existingItem.SaltCompositionId = request.SaltCompositionId;
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
                    SaltCompositionId = request.SaltCompositionId,
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

                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT 1 FROM InventoryItem WITH (UPDLOCK) WHERE InventoryItemId = {0} AND HospitalId = {1}", 
                    request.InventoryItemId, request.HospitalId);

                var item = await _context.InventoryItem
                    .FirstOrDefaultAsync(i => i.InventoryItemId == request.InventoryItemId && i.HospitalId == request.HospitalId, cancellationToken);
                if (item == null)
                    return new RecordInventoryMovementResponseModel { Success = false, Message = "Inventory item not found." };

                var isInbound = IpdConstants.InventoryMovementType.Inbound.Contains(movementType);
                var totalDelta = isInbound ? request.Qty : -request.Qty;

                if (item.CurrentStock + totalDelta < 0)
                    return new RecordInventoryMovementResponseModel { Success = false, Message = $"Insufficient stock — only {item.CurrentStock} {item.Unit} available." };

                if (!isInbound && !string.IsNullOrWhiteSpace(item.ScheduleClass))
                {
                    if (item.ScheduleClass == IpdConstants.DrugScheduleClass.Narcotic && !request.IsNarcoticDispenseContext)
                        return new RecordInventoryMovementResponseModel { Success = false, Message = "Narcotic items must be dispensed via the narcotics dispense endpoint, not a plain movement." };
                    if (string.IsNullOrWhiteSpace(request.PrescriberRef))
                        return new RecordInventoryMovementResponseModel { Success = false, Message = "A prescriber reference is required to dispense a scheduled drug." };
                    if (item.ScheduleClass == IpdConstants.DrugScheduleClass.Narcotic)
                    {
                        if (string.IsNullOrWhiteSpace(request.WitnessBy) || !request.WitnessByUserId.HasValue)
                            return new RecordInventoryMovementResponseModel { Success = false, Message = "A witness is required to dispense a narcotic." };
                        // Without this, the dispensing pharmacist could type their own name (or the
                        // caller could pass their own user id) as the "witness", defeating the entire
                        // point of NDPS dual-control -- confirmed live-reachable since nothing
                        // previously compared WitnessByUserId against the dispensing user.
                        if (request.LoggedInUserId.HasValue && request.WitnessByUserId.Value == request.LoggedInUserId.Value)
                            return new RecordInventoryMovementResponseModel { Success = false, Message = "The witness must be a different person from the dispensing user." };
                    }
                }

                var allocations = new List<BatchAllocation>();
                var today = DateTime.UtcNow.Date;

                if (request.BatchId.HasValue && request.BatchId != Guid.Empty)
                {
                    var singleBatch = await _context.Batch.FirstOrDefaultAsync(
                        b => b.BatchId == request.BatchId && b.HospitalId == request.HospitalId && b.InventoryItemId == item.InventoryItemId, cancellationToken);
                    
                    if (singleBatch == null)
                        return new RecordInventoryMovementResponseModel { Success = false, Message = "Batch not found." };

                    await _context.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM Batch WITH (UPDLOCK) WHERE BatchId = {0}", singleBatch.BatchId);

                    if (!isInbound && !request.IsVendorReturnContext && (singleBatch.Status != "ACTIVE" || (singleBatch.ExpiryDate.HasValue && singleBatch.ExpiryDate.Value.Date < today)))
                        return new RecordInventoryMovementResponseModel { Success = false, Message = $"Cannot dispense from batch {singleBatch.BatchNumber} — it is expired or no longer active." };

                    if (isInbound && request.StoreId.HasValue && request.StoreId.Value != Guid.Empty && singleBatch.StoreId != request.StoreId.Value)
                    {
                        var destBatch = await _context.Batch.FirstOrDefaultAsync(
                            b => b.StoreId == request.StoreId.Value && b.HospitalId == request.HospitalId 
                            && b.InventoryItemId == item.InventoryItemId && b.BatchNumber == singleBatch.BatchNumber, cancellationToken);
                        
                        if (destBatch != null)
                        {
                            await _context.Database.ExecuteSqlRawAsync(
                                "SELECT 1 FROM Batch WITH (UPDLOCK) WHERE BatchId = {0}", destBatch.BatchId);
                            singleBatch = destBatch;
                        }
                        else
                        {
                            destBatch = new Batch
                            {
                                BatchId = Guid.NewGuid(),
                                HospitalId = singleBatch.HospitalId,
                                InventoryItemId = singleBatch.InventoryItemId,
                                StoreId = request.StoreId.Value,
                                BatchNumber = singleBatch.BatchNumber,
                                ManufactureDate = singleBatch.ManufactureDate,
                                ExpiryDate = singleBatch.ExpiryDate,
                                UnitCost = singleBatch.UnitCost,
                                ReceivedQty = 0,
                                RemainingQty = 0,
                                VendorId = singleBatch.VendorId,
                                GrnLineId = singleBatch.GrnLineId,
                                Status = "ACTIVE",
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = request.LoggedInUserName,
                                UpdatedAt = DateTime.UtcNow,
                                UpdatedBy = request.LoggedInUserName,
                            };
                            _context.Batch.Add(destBatch);
                            singleBatch = destBatch;
                        }
                    }

                    if (!isInbound && singleBatch.RemainingQty + totalDelta < 0)
                        return new RecordInventoryMovementResponseModel { Success = false, Message = $"Insufficient stock in batch {singleBatch.BatchNumber} — only {singleBatch.RemainingQty} {item.Unit} remaining." };

                    allocations.Add(new BatchAllocation { Batch = singleBatch, AllocatedQty = request.Qty });
                }
                else if (!isInbound && request.StoreId.HasValue && request.StoreId != Guid.Empty)
                {
                    allocations = await FefoBatchAllocationService.AllocateAsync(_context, request.HospitalId, item.InventoryItemId, request.StoreId.Value, request.Qty, cancellationToken);
                    if (allocations == null || allocations.Count == 0)
                        return new RecordInventoryMovementResponseModel { Success = false, Message = "No active batch has enough remaining stock in that store to cover this quantity." };

                    foreach (var alloc in allocations)
                    {
                        await _context.Database.ExecuteSqlRawAsync(
                            "SELECT 1 FROM Batch WITH (UPDLOCK) WHERE BatchId = {0}", alloc.Batch.BatchId);
                    }
                }
                else
                {
                    allocations.Add(new BatchAllocation { Batch = null!, AllocatedQty = request.Qty });
                }

                var now = DateTime.UtcNow;
                var movementIds = new List<Guid>();
                var batchIds = new List<Guid>();
                var allocatedBatchDetails = new List<AllocatedBatchDetail>();

                foreach (var alloc in allocations)
                {
                    var batch = alloc.Batch;
                    var allocQty = alloc.AllocatedQty;
                    var allocDelta = isInbound ? allocQty : -allocQty;
                    var storeId = batch?.StoreId ?? request.StoreId;

                    StockLevel? stockLevel = null;
                    if (storeId.HasValue && storeId != Guid.Empty)
                    {
                        await _context.Database.ExecuteSqlRawAsync(
                            "SELECT 1 FROM StockLevel WITH (UPDLOCK) WHERE InventoryItemId = {0} AND StoreId = {1}", item.InventoryItemId, storeId.Value);

                        stockLevel = await _context.StockLevel.FirstOrDefaultAsync(
                            sl => sl.InventoryItemId == item.InventoryItemId && sl.StoreId == storeId, cancellationToken);
                        
                        var currentQty = stockLevel?.QtyOnHand ?? 0;
                        if (currentQty + allocDelta < 0)
                            return new RecordInventoryMovementResponseModel { Success = false, Message = "Insufficient stock at that store." };
                    }

                    var movement = new InventoryMovement
                    {
                        InventoryMovementId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        InventoryItemId = item.InventoryItemId,
                        MovementType = movementType,
                        Qty = allocQty,
                        UnitCost = request.UnitCost ?? batch?.UnitCost,
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
                    movementIds.Add(movement.InventoryMovementId);
                    
                    if (batch != null)
                    {
                        batchIds.Add(batch.BatchId);
                        batch.RemainingQty += allocDelta;
                        batch.UpdatedAt = now;
                        batch.UpdatedBy = request.LoggedInUserName;
                        if (batch.RemainingQty == 0 && batch.Status == "ACTIVE")
                            batch.Status = "EXHAUSTED";

                        if (!isInbound)
                        {
                            allocatedBatchDetails.Add(new AllocatedBatchDetail
                            {
                                BatchId = batch.BatchId,
                                BatchNumber = batch.BatchNumber,
                                ExpiryDate = batch.ExpiryDate,
                                Mrp = batch.Mrp,
                                AllocatedQty = allocQty
                            });
                        }
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
                        stockLevel.QtyOnHand += allocDelta;
                        stockLevel.UpdatedAt = now;
                    }

                    if (item.ScheduleClass == IpdConstants.DrugScheduleClass.Narcotic && batch != null && storeId.HasValue)
                    {
                        var formType = !string.IsNullOrWhiteSpace(request.PatientId)
                            ? IpdConstants.NarcoticFormType.Form3E
                            : IpdConstants.NarcoticFormType.Form3D;
                        if (formType == IpdConstants.NarcoticFormType.Form3D)
                        {
                            var storeType = await _context.Store.Where(s => s.StoreId == storeId).Select(s => s.StoreType).FirstOrDefaultAsync(cancellationToken);
                            if (storeType == IpdConstants.StoreType.Main || storeType == IpdConstants.StoreType.Narcotic)
                                formType = IpdConstants.NarcoticFormType.Form3H;
                        }

                        _context.NarcoticRegisterEntry.Add(new NarcoticRegisterEntry
                        {
                            RegisterEntryId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            InventoryItemId = item.InventoryItemId,
                            BatchId = batch.BatchId,
                            StoreId = storeId.Value,
                            FormType = formType,
                            Direction = isInbound ? IpdConstants.NarcoticDirection.In : IpdConstants.NarcoticDirection.Out,
                            Qty = allocQty,
                            BalanceAfter = batch.RemainingQty,
                            PatientId = request.PatientId,
                            EncounterId = request.EncounterId,
                            PrescriberRef = request.PrescriberRef,
                            IssuedBy = request.LoggedInUserName,
                            IssuedByUserId = request.LoggedInUserId,
                            WitnessBy = string.IsNullOrWhiteSpace(request.WitnessBy) ? (request.LoggedInUserName ?? "Unknown") : request.WitnessBy,
                            WitnessByUserId = request.WitnessByUserId,
                            RecordedAt = now,
                        });
                    }

                    // Schedule H1 register — Drugs & Cosmetics Rules: date/patient/prescriber/qty
                    // for every dispense, no witness required (unlike narcotics above).
                    if (item.ScheduleClass == IpdConstants.DrugScheduleClass.H1 && !isInbound && batch != null && storeId.HasValue)
                    {
                        _context.DrugScheduleRegisterEntry.Add(new DrugScheduleRegisterEntry
                        {
                            RegisterEntryId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            InventoryItemId = item.InventoryItemId,
                            BatchId = batch.BatchId,
                            StoreId = storeId.Value,
                            ScheduleClass = item.ScheduleClass,
                            Qty = allocQty,
                            PatientId = request.PatientId,
                            EncounterId = request.EncounterId,
                            PrescriberRef = request.PrescriberRef,
                            DispensedBy = request.LoggedInUserName,
                            DispensedByUserId = request.LoggedInUserId,
                            RecordedAt = now,
                        });
                    }
                }

                item.CurrentStock += totalDelta;
                item.UpdatedAt = now;
                item.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordInventoryMovementResponseModel
                {
                    Success = true,
                    Message = "Movement recorded.",
                    InventoryMovementId = movementIds.FirstOrDefault(),
                    InventoryMovementIds = movementIds,
                    NewCurrentStock = item.CurrentStock,
                    BatchId = batchIds.FirstOrDefault(),
                    BatchIds = batchIds,
                    AllocatedBatchDetails = allocatedBatchDetails
                };
            }
            catch (Exception ex)
            {
                return new RecordInventoryMovementResponseModel { Success = false, Message = $"Error recording inventory movement: {ex.Message}" };
            }
        }
    }
}
