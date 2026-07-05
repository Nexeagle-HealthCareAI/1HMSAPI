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
                    .FirstOrDefaultAsync(x => x.BedId == request.BedId && x.HospitalId == request.HospitalId, cancellationToken);

                if (existingBed == null)
                    throw new KeyNotFoundException($"Bed with ID {request.BedId} not found.");

                if (!string.IsNullOrEmpty(request.WardCode)) existingBed.WardCode = request.WardCode;
                if (!string.IsNullOrEmpty(request.WardName)) existingBed.WardName = request.WardName;
                if (!string.IsNullOrEmpty(request.WardType)) existingBed.WardType = request.WardType;
                if (!string.IsNullOrEmpty(request.FloorNo)) existingBed.FloorNo = request.FloorNo;
                if (!string.IsNullOrEmpty(request.RoomCode)) existingBed.RoomCode = request.RoomCode;
                if (!string.IsNullOrEmpty(request.RoomType)) existingBed.RoomType = request.RoomType;
                // Capacity is optional: store a positive value, otherwise NULL (DB CHECK forbids 0).
                existingBed.CapacityInRoom = request.CapacityInRoom > 0 ? request.CapacityInRoom : null;
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

            var wardCode = request.WardCode;
            var wardName = request.WardName;
            var wardType = request.WardType;
            var floorNo = request.FloorNo;
            var roomCode = request.RoomCode;
            var roomType = request.RoomType;
            var capacityInRoom = request.CapacityInRoom > 0 ? request.CapacityInRoom : null;
            var wardRoomDailyRate = request.WardRoomDailyRate;

            if (request.RoomId.HasValue && request.RoomId != Guid.Empty)
            {
                var room = await _context.Room
                    .FirstOrDefaultAsync(r => r.RoomId == request.RoomId && r.HospitalId == request.HospitalId, cancellationToken);
                if (room == null)
                    throw new KeyNotFoundException($"Room with ID {request.RoomId} not found.");

                var activeBedCount = await _context.BedMaster
                    .CountAsync(b => b.RoomId == room.RoomId && b.IsActive, cancellationToken);
                if (activeBedCount >= room.CapacityInRoom)
                    throw new InvalidOperationException($"Room {room.RoomNo} is already at its capacity of {room.CapacityInRoom} bed(s).");

                wardCode = room.WardCode;
                wardName = room.WardName;
                wardType = room.WardType;
                floorNo = room.FloorNo;
                roomCode = room.RoomNo;
                roomType = room.RoomType;
                capacityInRoom = room.CapacityInRoom;
                wardRoomDailyRate = room.DailyRate;
            }

            var bed = new BedMaster
            {
                BedId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                RoomId = request.RoomId,
                WardCode = wardCode,
                WardName = wardName,
                WardType = wardType,
                FloorNo = floorNo,
                RoomCode = roomCode,
                RoomType = roomType,
                CapacityInRoom = capacityInRoom,
                WardRoomDailyRate = wardRoomDailyRate,
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
