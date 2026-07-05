using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class RoomQueryHandlers :
        IRequestHandler<GetRoomsRequestModel, GetRoomsResponseModel>,
        IRequestHandler<GetRoomByIdRequestModel, RoomDetailResponseModel>
    {
        private readonly AppDbContext _context;

        public RoomQueryHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetRoomsResponseModel> Handle(GetRoomsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Room.Where(r => r.HospitalId == request.HospitalId);
            var totalCount = await query.CountAsync(cancellationToken);
            var rooms = await query
                .OrderBy(r => r.WardCode).ThenBy(r => r.RoomNo)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var roomIds = rooms.Select(r => r.RoomId).ToList();

            var bedCounts = (await _context.BedMaster
                    .Where(b => b.RoomId.HasValue && roomIds.Contains(b.RoomId.Value) && b.IsActive)
                    .GroupBy(b => b.RoomId!.Value)
                    .Select(g => new { RoomId = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.RoomId, x => x.Count);

            var occupiedCounts = (await (
                    from b in _context.BedMaster
                    join a in _context.BedAssignment on b.BedId equals a.BedId
                    where b.RoomId.HasValue && roomIds.Contains(b.RoomId.Value)
                          && a.StatusCode == IpdConstants.BedAssignmentStatus.Active
                    select b.RoomId!.Value)
                    .GroupBy(roomId => roomId)
                    .Select(g => new { RoomId = g.Key, Count = g.Count() })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.RoomId, x => x.Count);

            var items = rooms.Select(r => new RoomItemResponseModel
            {
                RoomId = r.RoomId,
                WardCode = r.WardCode,
                WardName = r.WardName,
                WardType = r.WardType,
                FloorNo = r.FloorNo,
                RoomNo = r.RoomNo,
                RoomType = r.RoomType,
                CapacityInRoom = r.CapacityInRoom,
                DailyRate = r.DailyRate,
                IsActive = r.IsActive,
                BedCount = bedCounts.TryGetValue(r.RoomId, out var bc) ? bc : 0,
                OccupiedBedCount = occupiedCounts.TryGetValue(r.RoomId, out var oc) ? oc : 0,
                UpdatedAt = r.UpdatedAt,
                UpdatedBy = r.UpdatedBy
            }).ToList();

            return new GetRoomsResponseModel
            {
                Items = items,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<RoomDetailResponseModel> Handle(GetRoomByIdRequestModel request, CancellationToken cancellationToken)
        {
            var r = await _context.Room
                .FirstOrDefaultAsync(x => x.RoomId == request.RoomId && x.HospitalId == request.HospitalId, cancellationToken);
            if (r == null) return new RoomDetailResponseModel();

            var beds = await _context.BedMaster
                .Where(b => b.RoomId == r.RoomId)
                .OrderBy(b => b.SortOrder)
                .Select(b => new BedMasterItemResponseModel
                {
                    BedId = b.BedId,
                    WardCode = b.WardCode,
                    WardName = b.WardName,
                    WardType = b.WardType,
                    FloorNo = b.FloorNo,
                    RoomCode = b.RoomCode,
                    RoomType = b.RoomType,
                    CapacityInRoom = b.CapacityInRoom,
                    WardRoomDailyRate = b.WardRoomDailyRate,
                    BedDailyRateOverride = b.BedDailyRateOverride,
                    EffectiveDailyRate = b.BedDailyRateOverride ?? b.WardRoomDailyRate,
                    IncentiveAmount = b.IncentiveAmount,
                    BedCode = b.BedCode,
                    BedName = b.BedName,
                    StatusCode = b.StatusCode,
                    GenderRestriction = b.GenderRestriction,
                    IsActive = b.IsActive,
                    SortOrder = b.SortOrder,
                    LastStatusAt = b.LastStatusAt,
                    UpdatedAt = b.UpdatedAt,
                    UpdatedBy = b.UpdatedBy
                })
                .ToListAsync(cancellationToken);

            var activeBedIds = beds.Where(b => b.IsActive).Select(b => b.BedId).ToList();
            var occupiedCount = activeBedIds.Count == 0 ? 0 : await _context.BedAssignment
                .CountAsync(a => activeBedIds.Contains(a.BedId) && a.StatusCode == IpdConstants.BedAssignmentStatus.Active, cancellationToken);

            return new RoomDetailResponseModel
            {
                RoomId = r.RoomId,
                HospitalId = r.HospitalId,
                WardCode = r.WardCode,
                WardName = r.WardName,
                WardType = r.WardType,
                FloorNo = r.FloorNo,
                RoomNo = r.RoomNo,
                RoomType = r.RoomType,
                CapacityInRoom = r.CapacityInRoom,
                DailyRate = r.DailyRate,
                IsActive = r.IsActive,
                BedCount = beds.Count(b => b.IsActive),
                OccupiedBedCount = occupiedCount,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy,
                UpdatedAt = r.UpdatedAt,
                UpdatedBy = r.UpdatedBy,
                Beds = beds
            };
        }
    }
}
