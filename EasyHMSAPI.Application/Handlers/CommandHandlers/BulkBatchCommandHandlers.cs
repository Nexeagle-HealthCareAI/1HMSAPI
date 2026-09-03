using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class BulkBatchCommandHandlers : IRequestHandler<CreateBulkBatchRequestModel, CreateBulkBatchResponseModel>
    {
        private readonly AppDbContext _context;

        public BulkBatchCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateBulkBatchResponseModel> Handle(CreateBulkBatchRequestModel request, CancellationToken cancellationToken)
        {
            // A plain _context.Database.BeginTransactionAsync() here throws at runtime against a
            // real SQL Server connection ("SqlServerRetryingExecutionStrategy does not support
            // user-initiated transactions") — the retrying execution strategy must own the
            // transaction, same pattern PharmacyRetailCheckoutCommandHandler already uses. Never
            // caught by the in-memory-provider unit tests since InMemory has no execution strategy.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(() => TryHandleAsync(request, cancellationToken));
        }

        private async Task<CreateBulkBatchResponseModel> TryHandleAsync(CreateBulkBatchRequestModel request, CancellationToken cancellationToken)
        {
            var response = new CreateBulkBatchResponseModel { Success = true, TotalProcessed = request.Rows.Count };

            if (request.HospitalId == Guid.Empty)
            {
                response.Success = false;
                response.Message = "HospitalId is required.";
                return response;
            }

            if (request.Rows == null || !request.Rows.Any())
            {
                response.Success = false;
                response.Message = "No rows to process.";
                return response;
            }

            // Load lookups
            var stores = await _context.Store
                .Where(s => s.HospitalId == request.HospitalId)
                .ToDictionaryAsync(s => s.StoreCode.ToUpperInvariant(), s => s.StoreId, cancellationToken);

            // To map items by code, we might need all item codes used in the request
            var itemCodes = request.Rows.Select(r => r.ItemCode?.Trim().ToUpperInvariant()).Distinct().ToList();
            
            var items = await _context.InventoryItem
                .Where(i => i.HospitalId == request.HospitalId && itemCodes.Contains(i.ItemCode.ToUpper()))
                .ToDictionaryAsync(i => i.ItemCode.ToUpperInvariant(), i => i.InventoryItemId, cancellationToken);

            var now = DateTime.UtcNow;

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                for (int i = 0; i < request.Rows.Count; i++)
                {
                    var row = request.Rows[i];

                    if (string.IsNullOrWhiteSpace(row.StoreCode) || string.IsNullOrWhiteSpace(row.ItemCode) || string.IsNullOrWhiteSpace(row.BatchNumber))
                    {
                        response.Errors.Add(new BulkBatchRowError { RowIndex = i, ErrorMessage = "Store Code, Item Code, and Batch No are required." });
                        continue;
                    }

                    if (row.ReceivedQty <= 0)
                    {
                        response.Errors.Add(new BulkBatchRowError { RowIndex = i, ErrorMessage = "Qty must be greater than zero." });
                        continue;
                    }

                    var storeCode = row.StoreCode.Trim().ToUpperInvariant();
                    var itemCode = row.ItemCode.Trim().ToUpperInvariant();

                    if (!stores.TryGetValue(storeCode, out var storeId))
                    {
                        response.Errors.Add(new BulkBatchRowError { RowIndex = i, ErrorMessage = $"Store Code '{storeCode}' not found." });
                        continue;
                    }

                    if (!items.TryGetValue(itemCode, out var inventoryItemId))
                    {
                        response.Errors.Add(new BulkBatchRowError { RowIndex = i, ErrorMessage = $"Item Code '{itemCode}' not found." });
                        continue;
                    }

                    var batch = new Batch
                    {
                        BatchId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        InventoryItemId = inventoryItemId,
                        StoreId = storeId,
                        BatchNumber = row.BatchNumber.Trim(),
                        ManufactureDate = row.ManufactureDate,
                        ExpiryDate = row.ExpiryDate,
                        UnitCost = row.UnitCost,
                        Mrp = row.Mrp,
                        BarcodeValue = string.IsNullOrWhiteSpace(row.BarcodeValue) ? null : row.BarcodeValue.Trim(),
                        ReceivedQty = row.ReceivedQty,
                        RemainingQty = row.ReceivedQty,
                        Status = "ACTIVE",
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName
                    };

                    _context.Batch.Add(batch);

                    var stockLevel = await _context.StockLevel.FirstOrDefaultAsync(
                        sl => sl.InventoryItemId == inventoryItemId && sl.StoreId == storeId && sl.HospitalId == request.HospitalId, cancellationToken);

                    if (stockLevel == null)
                    {
                        stockLevel = new StockLevel
                        {
                            StockLevelId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            InventoryItemId = inventoryItemId,
                            StoreId = storeId,
                            QtyOnHand = batch.ReceivedQty,
                            UpdatedAt = now
                        };
                        _context.StockLevel.Add(stockLevel);
                    }
                    else
                    {
                        stockLevel.QtyOnHand += batch.ReceivedQty;
                        stockLevel.UpdatedAt = now;
                    }

                    // Bulk import bypassed both InventoryItem.CurrentStock and the InventoryMovement
                    // audit trail until now — CurrentStock drove low-stock alerts and InventoryMovement
                    // feeds the reorder-threshold suggestion engine, so a bulk-imported batch was
                    // invisible to both. Kept as plain field/row writes (not routed through
                    // RecordInventoryMovementRequestModel) since this handler already holds its own
                    // transaction and item/store locks aren't needed for a straight RECEIVE.
                    var item = await _context.InventoryItem.FirstOrDefaultAsync(
                        it => it.InventoryItemId == inventoryItemId && it.HospitalId == request.HospitalId, cancellationToken);
                    if (item != null)
                    {
                        item.CurrentStock += batch.ReceivedQty;
                        item.UpdatedAt = now;
                        item.UpdatedBy = request.LoggedInUserName;
                    }

                    _context.InventoryMovement.Add(new InventoryMovement
                    {
                        InventoryMovementId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        InventoryItemId = inventoryItemId,
                        MovementType = "RECEIVE",
                        Qty = batch.ReceivedQty,
                        UnitCost = batch.UnitCost,
                        BatchId = batch.BatchId,
                        BatchNumber = batch.BatchNumber,
                        ExpiryDate = batch.ExpiryDate,
                        ToStoreId = storeId,
                        SourceModule = "BULK_IMPORT",
                        MovedAt = now,
                        MovedBy = request.LoggedInUserName,
                        CreatedAt = now,
                    });

                    response.SuccessCount++;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            if (response.Errors.Any())
            {
                response.Message = $"Processed {response.TotalProcessed} rows. {response.SuccessCount} succeeded, {response.Errors.Count} failed.";
                if (response.SuccessCount == 0) response.Success = false;
            }
            else
            {
                response.Message = $"Successfully processed all {response.SuccessCount} rows.";
            }

            return response;
        }
    }
}
