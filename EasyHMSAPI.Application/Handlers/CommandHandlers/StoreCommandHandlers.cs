using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class StoreCommandHandlers : IRequestHandler<UpsertStoreRequestModel, UpsertStoreResponseModel>
    {
        private readonly AppDbContext _context;

        public StoreCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertStoreResponseModel> Handle(UpsertStoreRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.StoreCode) || string.IsNullOrWhiteSpace(request.StoreName))
                    return new UpsertStoreResponseModel { Success = false, Message = "HospitalId, StoreCode, and StoreName are required." };
                if (string.IsNullOrWhiteSpace(request.StoreType) || !IpdConstants.StoreType.All.Contains(request.StoreType))
                    return new UpsertStoreResponseModel { Success = false, Message = "Invalid store type." };

                if (request.ParentStoreId.HasValue && request.ParentStoreId != Guid.Empty)
                {
                    var parentExists = await _context.Store.AnyAsync(
                        s => s.StoreId == request.ParentStoreId && s.HospitalId == request.HospitalId, cancellationToken);
                    if (!parentExists)
                        return new UpsertStoreResponseModel { Success = false, Message = "Parent store not found." };
                    if (request.StoreId.HasValue && request.ParentStoreId == request.StoreId)
                        return new UpsertStoreResponseModel { Success = false, Message = "A store cannot be its own parent." };
                }

                var now = DateTime.UtcNow;

                if (request.StoreId.HasValue && request.StoreId != Guid.Empty)
                {
                    var existingStore = await _context.Store
                        .FirstOrDefaultAsync(s => s.StoreId == request.StoreId && s.HospitalId == request.HospitalId, cancellationToken);
                    if (existingStore == null)
                        return new UpsertStoreResponseModel { Success = false, Message = "Store not found." };

                    var codeTaken = await _context.Store.AnyAsync(
                        s => s.HospitalId == request.HospitalId && s.StoreCode == request.StoreCode.Trim() && s.StoreId != existingStore.StoreId, cancellationToken);
                    if (codeTaken)
                        return new UpsertStoreResponseModel { Success = false, Message = "A store with this code already exists." };

                    existingStore.StoreCode = request.StoreCode.Trim();
                    existingStore.StoreName = request.StoreName.Trim();
                    existingStore.StoreType = request.StoreType;
                    existingStore.ParentStoreId = request.ParentStoreId;
                    existingStore.MinTempCelsius = request.MinTempCelsius;
                    existingStore.MaxTempCelsius = request.MaxTempCelsius;
                    existingStore.IsActive = request.IsActive;
                    existingStore.UpdatedAt = now;
                    existingStore.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);
                    return new UpsertStoreResponseModel { Success = true, Message = "Store updated.", StoreId = existingStore.StoreId };
                }

                var exists = await _context.Store.AnyAsync(
                    s => s.HospitalId == request.HospitalId && s.StoreCode == request.StoreCode.Trim(), cancellationToken);
                if (exists)
                    return new UpsertStoreResponseModel { Success = false, Message = "A store with this code already exists." };

                var store = new Store
                {
                    StoreId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    StoreCode = request.StoreCode.Trim(),
                    StoreName = request.StoreName.Trim(),
                    StoreType = request.StoreType,
                    ParentStoreId = request.ParentStoreId,
                    MinTempCelsius = request.MinTempCelsius,
                    MaxTempCelsius = request.MaxTempCelsius,
                    IsActive = request.IsActive,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.Store.Add(store);
                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertStoreResponseModel { Success = true, Message = "Store created.", StoreId = store.StoreId };
            }
            catch (Exception)
            {
                return new UpsertStoreResponseModel { Success = false, Message = "Error saving store." };
            }
        }
    }
}
