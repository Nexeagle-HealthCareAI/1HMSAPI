using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertBedMasterHandler : IRequestHandler<UpsertBedMasterRequestModel, UpsertBedMasterResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertBedMasterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertBedMasterResponseModel> Handle(UpsertBedMasterRequestModel request, CancellationToken cancellationToken)
        {
            if (request.BedId != null && request.BedId != Guid.Empty)
            {
                var existingBed = await _context.BedMaster
                    .FirstOrDefaultAsync(x => x.BedId == request.BedId, cancellationToken);

                if (existingBed == null)
                    throw new KeyNotFoundException($"Bed with ID {request.BedId} not found.");

                if (!string.IsNullOrEmpty(request.WardCode)) existingBed.WardCode = request.WardCode;
                if (!string.IsNullOrEmpty(request.WardName)) existingBed.WardName = request.WardName;
                if (!string.IsNullOrEmpty(request.WardType)) existingBed.WardType = request.WardType;
                if (!string.IsNullOrEmpty(request.FloorNo)) existingBed.FloorNo = request.FloorNo;
                if (!string.IsNullOrEmpty(request.RoomCode)) existingBed.RoomCode = request.RoomCode;
                if (!string.IsNullOrEmpty(request.RoomType)) existingBed.RoomType = request.RoomType;
                if (request.CapacityInRoom > 0) existingBed.CapacityInRoom = request.CapacityInRoom;
                if (request.WardRoomDailyRate > 0) existingBed.WardRoomDailyRate = request.WardRoomDailyRate;
                existingBed.BedDailyRateOverride = request.BedDailyRateOverride;
                existingBed.IncentiveAmount = request.IncentiveAmount;
                if (!string.IsNullOrEmpty(request.BedCode)) existingBed.BedCode = request.BedCode;
                if (!string.IsNullOrEmpty(request.BedName)) existingBed.BedName = request.BedName;
                if (!string.IsNullOrEmpty(request.StatusCode)) existingBed.StatusCode = request.StatusCode;
                if (!string.IsNullOrEmpty(request.GenderRestriction)) existingBed.GenderRestriction = request.GenderRestriction;
                existingBed.IsActive = request.IsActive;
                if (request.SortOrder > 0) existingBed.SortOrder = request.SortOrder;
                existingBed.UpdatedAt = DateTime.UtcNow;
                existingBed.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertBedMasterResponseModel
                {
                    BedId = existingBed.BedId,
                    BedCode = existingBed.BedCode,
                    UpdatedAt = existingBed.UpdatedAt,
                    UpdatedBy = existingBed.UpdatedBy
                };
            }

            var bed = new BedMaster
            {
                BedId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                WardCode = request.WardCode,
                WardName = request.WardName,
                WardType = request.WardType,
                FloorNo = request.FloorNo,
                RoomCode = request.RoomCode,
                RoomType = request.RoomType,
                CapacityInRoom = request.CapacityInRoom,
                WardRoomDailyRate = request.WardRoomDailyRate,
                BedDailyRateOverride = request.BedDailyRateOverride,
                IncentiveAmount = request.IncentiveAmount,
                BedCode = request.BedCode,
                BedName = request.BedName,
                StatusCode = request.StatusCode,
                GenderRestriction = request.GenderRestriction,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                LastStatusAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.LoggedInUserName,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = request.LoggedInUserName
            };

            _context.BedMaster.Add(bed);
            await _context.SaveChangesAsync(cancellationToken);

            return new UpsertBedMasterResponseModel
            {
                BedId = bed.BedId,
                BedCode = bed.BedCode,
                UpdatedAt = bed.UpdatedAt,
                UpdatedBy = bed.UpdatedBy
            };
        }
    }
}
