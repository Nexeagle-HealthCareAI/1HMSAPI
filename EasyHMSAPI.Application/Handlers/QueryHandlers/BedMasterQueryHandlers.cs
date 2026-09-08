using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class BedMasterQueryHandlers :
        IRequestHandler<GetBedMastersRequestModel, GetBedMastersResponseModel>,
        IRequestHandler<GetBedMasterByIdRequestModel, BedMasterDetailResponseModel>
    {
        private readonly AppDbContext _context;

        public BedMasterQueryHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBedMastersResponseModel> Handle(GetBedMastersRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.BedMaster.Where(b => b.HospitalId == request.HospitalId);
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(b => b.SortOrder)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(b => new BedMasterItemResponseModel
                {
                    BedId = b.BedId,
                    RoomId = b.RoomId,
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

            return new GetBedMastersResponseModel
            {
                Items = items,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<BedMasterDetailResponseModel> Handle(GetBedMasterByIdRequestModel request, CancellationToken cancellationToken)
        {
            var b = await _context.BedMaster
                .FirstOrDefaultAsync(x => x.BedId == request.BedId && x.HospitalId == request.HospitalId, cancellationToken);

            if (b == null) return new BedMasterDetailResponseModel();

            return new BedMasterDetailResponseModel
            {
                BedId = b.BedId,
                RoomId = b.RoomId,
                HospitalId = b.HospitalId,
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
                CreatedAt = b.CreatedAt,
                CreatedBy = b.CreatedBy,
                UpdatedAt = b.UpdatedAt,
                UpdatedBy = b.UpdatedBy
            };
        }
    }
}
