using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Intake/output entries for an admission plus IST-day-bucketed totals — same
    /// "bulk-load raw rows, fold in-memory" style as GetMarGridHandler, no stored denormalization.</summary>
    public class GetFluidBalanceHandler : IRequestHandler<GetFluidBalanceRequestModel, GetFluidBalanceResponseModel>
    {
        private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

        private readonly AppDbContext _context;

        public GetFluidBalanceHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetFluidBalanceResponseModel> Handle(GetFluidBalanceRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetFluidBalanceResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var toUtc = request.ToUtc ?? DateTime.UtcNow;
                var fromUtc = request.FromUtc ?? toUtc.AddDays(-7);

                var entries = await _context.FluidEntry
                    .Where(f => f.HospitalId == request.HospitalId && f.AdmissionId == request.AdmissionId
                        && f.RecordedAt >= fromUtc && f.RecordedAt <= toUtc)
                    .OrderByDescending(f => f.RecordedAt)
                    .ToListAsync(cancellationToken);

                var items = entries.Select(f => new FluidEntryItem
                {
                    FluidEntryId = f.FluidEntryId,
                    Direction = f.Direction,
                    Subtype = f.Subtype,
                    VolumeMl = f.VolumeMl,
                    Description = f.Description,
                    RouteOrSite = f.RouteOrSite,
                    Colour = f.Colour,
                    RecordedAt = f.RecordedAt,
                    RecordedBy = f.RecordedBy,
                    Notes = f.Notes,
                }).ToList();

                var dailyTotals = entries
                    .GroupBy(f => (f.RecordedAt + IstOffset).ToString("yyyy-MM-dd"))
                    .Select(g => new FluidDayTotal
                    {
                        DayKey = g.Key,
                        TotalInMl = g.Where(e => e.Direction == "IN").Sum(e => e.VolumeMl),
                        TotalOutMl = g.Where(e => e.Direction == "OUT").Sum(e => e.VolumeMl),
                        BalanceMl = g.Where(e => e.Direction == "IN").Sum(e => e.VolumeMl) - g.Where(e => e.Direction == "OUT").Sum(e => e.VolumeMl),
                    })
                    .OrderByDescending(d => d.DayKey)
                    .ToList();

                return new GetFluidBalanceResponseModel { Success = true, Entries = items, DailyTotals = dailyTotals };
            }
            catch (Exception)
            {
                return new GetFluidBalanceResponseModel { Success = false, Message = "Error loading fluid balance." };
            }
        }
    }
}
