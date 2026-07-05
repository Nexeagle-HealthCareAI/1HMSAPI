using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class BulkCreateBedMasterHandler : IRequestHandler<BulkCreateBedMasterRequestModel, BulkCreateBedMasterResponseModel>
    {
        private const int MaxBulk = 200;
        private readonly AppDbContext _context;

        public BulkCreateBedMasterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<BulkCreateBedMasterResponseModel> Handle(BulkCreateBedMasterRequestModel request, CancellationToken cancellationToken)
        {
            if (request.Count <= 0)
                return new BulkCreateBedMasterResponseModel { Success = false, Message = "Number of beds must be greater than 0." };
            if (request.Count > MaxBulk)
                return new BulkCreateBedMasterResponseModel { Success = false, Message = $"Cannot create more than {MaxBulk} beds at once." };

            var wardCode = request.WardCode;
            var wardName = request.WardName;
            var wardType = request.WardType;
            var floorNo = request.FloorNo;
            var roomCode = request.RoomCode;
            var roomType = request.RoomType;
            var capacityInRoomOverride = request.CapacityInRoom;
            var wardRoomDailyRate = request.WardRoomDailyRate;

            if (request.RoomId.HasValue && request.RoomId != Guid.Empty)
            {
                var room = await _context.Room
                    .FirstOrDefaultAsync(r => r.RoomId == request.RoomId && r.HospitalId == request.HospitalId, cancellationToken);
                if (room == null)
                    return new BulkCreateBedMasterResponseModel { Success = false, Message = "Room not found." };

                var activeBedCount = await _context.BedMaster
                    .CountAsync(b => b.RoomId == room.RoomId && b.IsActive, cancellationToken);
                var remainingCapacity = room.CapacityInRoom - activeBedCount;
                if (request.Count > remainingCapacity)
                {
                    return new BulkCreateBedMasterResponseModel
                    {
                        Success = false,
                        Message = remainingCapacity <= 0
                            ? $"Room {room.RoomNo} is already at its capacity of {room.CapacityInRoom} bed(s)."
                            : $"Room {room.RoomNo} has room for {remainingCapacity} more bed(s), not {request.Count}."
                    };
                }

                wardCode = room.WardCode;
                wardName = room.WardName;
                wardType = room.WardType;
                floorNo = room.FloorNo;
                roomCode = room.RoomNo;
                roomType = room.RoomType;
                capacityInRoomOverride = room.CapacityInRoom;
                wardRoomDailyRate = room.DailyRate;
            }

            if (string.IsNullOrWhiteSpace(wardCode))
                return new BulkCreateBedMasterResponseModel { Success = false, Message = "Ward code is required." };

            var prefix = (request.BedCodePrefix ?? wardType ?? "BED").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "BED";

            // Find the highest existing number (and pad width) for this prefix so new codes continue
            // the sequence and never collide with the UX_BM_Code unique constraint.
            var existingCodes = await _context.BedMaster
                .Where(b => b.HospitalId == request.HospitalId && b.BedCode != null && b.BedCode.StartsWith(prefix + "-"))
                .Select(b => b.BedCode!)
                .ToListAsync(cancellationToken);

            var re = new Regex($"^{Regex.Escape(prefix)}-(\\d+)$", RegexOptions.IgnoreCase);
            int max = 0;
            int width = 2;
            foreach (var code in existingCodes)
            {
                var m = re.Match(code);
                if (m.Success)
                {
                    max = Math.Max(max, int.Parse(m.Groups[1].Value));
                    width = Math.Max(width, m.Groups[1].Value.Length);
                }
            }

            var now = DateTime.UtcNow;
            var capacity = capacityInRoomOverride > 0 ? capacityInRoomOverride : null;
            var statusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "AVAILABLE" : request.StatusCode;

            var beds = new List<BedMaster>(request.Count);
            for (var i = 1; i <= request.Count; i++)
            {
                var num = max + i;
                var code = $"{prefix}-{num.ToString().PadLeft(width, '0')}";
                beds.Add(new BedMaster
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
                    CapacityInRoom = capacity,
                    WardRoomDailyRate = wardRoomDailyRate,
                    BedDailyRateOverride = request.BedDailyRateOverride,
                    IncentiveAmount = request.IncentiveAmount,
                    BedCode = code,
                    StatusCode = statusCode,
                    GenderRestriction = request.GenderRestriction,
                    IsActive = request.IsActive,
                    SortOrder = num,
                    LastStatusAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                });
            }

            _context.BedMaster.AddRange(beds);
            await _context.SaveChangesAsync(cancellationToken);

            return new BulkCreateBedMasterResponseModel
            {
                Success = true,
                CreatedCount = beds.Count,
                FirstBedCode = beds[0].BedCode,
                LastBedCode = beds[^1].BedCode,
                Message = $"{beds.Count} beds created ({beds[0].BedCode} – {beds[^1].BedCode})."
            };
        }
    }
}
