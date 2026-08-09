using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Distinct wards for a hospital, derived from BedMaster -- there is no separate Ward table.
    // Used by both the Nursing Station roster ward-picker and its ward chips.
    public class GetWardListHandler : IRequestHandler<GetWardListRequestModel, GetWardListResponseModel>
    {
        private readonly AppDbContext _context;

        public GetWardListHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetWardListResponseModel> Handle(GetWardListRequestModel request, CancellationToken cancellationToken)
        {
            var beds = await _context.BedMaster.AsNoTracking()
                .Where(b => b.HospitalId == request.HospitalId && b.IsActive && b.WardCode != null)
                .ToListAsync(cancellationToken);

            var wards = beds
                .GroupBy(b => b.WardCode!)
                .Select(g => new WardListItem
                {
                    WardCode = g.Key,
                    WardName = g.First().WardName,
                    WardType = g.First().WardType,
                    BedCount = g.Count(),
                })
                .OrderBy(w => w.WardName ?? w.WardCode)
                .ToList();

            return new GetWardListResponseModel { Wards = wards };
        }
    }
}
