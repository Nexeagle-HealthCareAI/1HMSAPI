using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertRoomHandler : IRequestHandler<UpsertRoomRequestModel, UpsertRoomResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertRoomHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertRoomResponseModel> Handle(UpsertRoomRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.FloorNo))
                return new UpsertRoomResponseModel { Success = false, Message = "Floor is required." };
            if (string.IsNullOrWhiteSpace(request.RoomNo))
                return new UpsertRoomResponseModel { Success = false, Message = "Room number is required." };
            if (request.CapacityInRoom <= 0)
                return new UpsertRoomResponseModel { Success = false, Message = "Capacity must be at least 1." };

            var now = DateTime.UtcNow;

            if (request.RoomId != null && request.RoomId != Guid.Empty)
            {
                var existingRoom = await _context.Room
                    .FirstOrDefaultAsync(r => r.RoomId == request.RoomId && r.HospitalId == request.HospitalId, cancellationToken);
                if (existingRoom == null)
                    return new UpsertRoomResponseModel { Success = false, Message = "Room not found." };

                // Never let the capacity drop below the beds already sitting in this room.
                var activeBedCount = await _context.BedMaster
                    .CountAsync(b => b.RoomId == existingRoom.RoomId && b.IsActive, cancellationToken);
                if (request.CapacityInRoom < activeBedCount)
                {
                    return new UpsertRoomResponseModel
                    {
                        Success = false,
                        Message = $"This room already has {activeBedCount} active bed(s) — capacity cannot be set below that."
                    };
                }

                existingRoom.WardCode = request.WardCode;
                existingRoom.WardName = request.WardName;
                existingRoom.WardType = request.WardType;
                existingRoom.FloorNo = request.FloorNo.Trim();
                existingRoom.RoomNo = request.RoomNo.Trim();
                existingRoom.RoomType = request.RoomType;
                existingRoom.CapacityInRoom = request.CapacityInRoom;
                existingRoom.DailyRate = request.DailyRate;
                existingRoom.IsActive = request.IsActive;
                existingRoom.UpdatedAt = now;
                existingRoom.UpdatedBy = request.LoggedInUserName;

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    return new UpsertRoomResponseModel { Success = false, Message = $"Room '{request.RoomNo}' already exists on floor '{request.FloorNo}'." };
                }

                return new UpsertRoomResponseModel
                {
                    Success = true,
                    Message = "Room updated.",
                    RoomId = existingRoom.RoomId,
                    RoomNo = existingRoom.RoomNo,
                    UpdatedAt = existingRoom.UpdatedAt,
                    UpdatedBy = existingRoom.UpdatedBy
                };
            }

            var room = new Room
            {
                RoomId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                WardCode = request.WardCode,
                WardName = request.WardName,
                WardType = request.WardType,
                FloorNo = request.FloorNo.Trim(),
                RoomNo = request.RoomNo.Trim(),
                RoomType = request.RoomType,
                CapacityInRoom = request.CapacityInRoom,
                DailyRate = request.DailyRate,
                IsActive = request.IsActive,
                CreatedAt = now,
                CreatedBy = request.LoggedInUserName,
                UpdatedAt = now,
                UpdatedBy = request.LoggedInUserName
            };

            try
            {
                _context.Room.Add(room);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return new UpsertRoomResponseModel { Success = false, Message = $"Room '{request.RoomNo}' already exists on floor '{request.FloorNo}'." };
            }

            return new UpsertRoomResponseModel
            {
                Success = true,
                Message = "Room created.",
                RoomId = room.RoomId,
                RoomNo = room.RoomNo,
                UpdatedAt = room.UpdatedAt,
                UpdatedBy = room.UpdatedBy
            };
        }
    }
}
