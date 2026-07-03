using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetSterilizationCycleHistoryHandler : IRequestHandler<GetSterilizationCycleHistoryRequestModel, GetSterilizationCycleHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetSterilizationCycleHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetSterilizationCycleHistoryResponseModel> Handle(GetSterilizationCycleHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var cycles = await _context.SterilizationCycle
                .Where(c => c.HospitalId == request.HospitalId)
                .OrderByDescending(c => c.StartedAt)
                .Take(request.Take)
                .ToListAsync(cancellationToken);

            var cycleIds = cycles.Select(c => c.SterilizationCycleId).ToList();
            var cycleItems = await _context.SterilizationCycleItem
                .Where(i => cycleIds.Contains(i.SterilizationCycleId))
                .ToListAsync(cancellationToken);

            var setIds = cycleItems.Select(i => i.InstrumentSetId).Distinct().ToList();
            var setsById = await _context.InstrumentSet
                .Where(s => setIds.Contains(s.InstrumentSetId))
                .ToDictionaryAsync(s => s.InstrumentSetId, cancellationToken);

            var itemsByCycle = cycleItems.GroupBy(i => i.SterilizationCycleId).ToDictionary(g => g.Key, g => g.ToList());

            var items = cycles.Select(c =>
            {
                var setCodes = itemsByCycle.TryGetValue(c.SterilizationCycleId, out var linkedItems)
                    ? linkedItems.Select(i => setsById.TryGetValue(i.InstrumentSetId, out var set) ? set.SetCode : null).Where(code => code != null).Select(code => code!).ToList()
                    : new List<string>();

                return new SterilizationCycleDataModel
                {
                    SterilizationCycleId = c.SterilizationCycleId,
                    CycleNumber = c.CycleNumber,
                    AutoclaveLabel = c.AutoclaveLabel,
                    CycleType = c.CycleType,
                    StartedAt = c.StartedAt,
                    EndedAt = c.EndedAt,
                    BiologicalIndicatorResult = c.BiologicalIndicatorResult,
                    ChemicalIndicatorResult = c.ChemicalIndicatorResult,
                    OperatorName = c.OperatorName,
                    SetCodes = setCodes,
                };
            }).ToList();

            return new GetSterilizationCycleHistoryResponseModel { Cycles = items };
        }
    }
}
