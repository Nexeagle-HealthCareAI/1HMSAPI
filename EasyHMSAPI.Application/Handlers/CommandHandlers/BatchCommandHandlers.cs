using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class BatchCommandHandlers : IRequestHandler<CreateBatchRequestModel, CreateBatchResponseModel>
    {
        private readonly AppDbContext _context;

        public BatchCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateBatchResponseModel> Handle(CreateBatchRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.InventoryItemId == Guid.Empty || request.StoreId == Guid.Empty)
                    return new CreateBatchResponseModel { Success = false, Message = "HospitalId, InventoryItemId, and StoreId are required." };
                if (string.IsNullOrWhiteSpace(request.BatchNumber))
                    return new CreateBatchResponseModel { Success = false, Message = "BatchNumber is required." };
                if (request.ReceivedQty <= 0)
                    return new CreateBatchResponseModel { Success = false, Message = "ReceivedQty must be greater than zero." };

                var itemExists = await _context.InventoryItem.AnyAsync(
                    i => i.InventoryItemId == request.InventoryItemId && i.HospitalId == request.HospitalId, cancellationToken);
                if (!itemExists)
                    return new CreateBatchResponseModel { Success = false, Message = "Inventory item not found." };

                var storeExists = await _context.Store.AnyAsync(
                    s => s.StoreId == request.StoreId && s.HospitalId == request.HospitalId, cancellationToken);
                if (!storeExists)
                    return new CreateBatchResponseModel { Success = false, Message = "Store not found." };

                var now = DateTime.UtcNow;
                var batch = new Batch
                {
                    BatchId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    InventoryItemId = request.InventoryItemId,
                    StoreId = request.StoreId,
                    BatchNumber = request.BatchNumber.Trim(),
                    ManufactureDate = request.ManufactureDate,
                    ExpiryDate = request.ExpiryDate,
                    UnitCost = request.UnitCost,
                    Mrp = request.Mrp,
                    BarcodeValue = string.IsNullOrWhiteSpace(request.BarcodeValue) ? null : request.BarcodeValue.Trim(),
                    ReceivedQty = request.ReceivedQty,
                    RemainingQty = 0,
                    VendorId = request.VendorId,
                    GrnLineId = request.GrnLineId,
                    Status = "ACTIVE",
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.Batch.Add(batch);
                await _context.SaveChangesAsync(cancellationToken);

                return new CreateBatchResponseModel { Success = true, Message = "Batch created.", BatchId = batch.BatchId };
            }
            catch (Exception)
            {
                return new CreateBatchResponseModel { Success = false, Message = "Error creating batch." };
            }
        }
    }
}
