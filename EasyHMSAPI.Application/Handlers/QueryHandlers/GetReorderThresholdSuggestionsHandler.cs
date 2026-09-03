using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Weekly/monthly auto-threshold suggestion: trailing 4-week ISSUE consumption / 4 = weekly
    // average; SuggestedMin = weeklyAverage x BufferMultiplier; SuggestedMax = SuggestedMin x 3
    // (~a month's cover). Read-only — AcceptThresholdSuggestionHandler is the only thing that writes.
    public class GetReorderThresholdSuggestionsHandler : IRequestHandler<GetReorderThresholdSuggestionsRequestModel, GetReorderThresholdSuggestionsResponseModel>
    {
        private readonly AppDbContext _context;
        private const int TrailingWindowDays = 28;

        public GetReorderThresholdSuggestionsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetReorderThresholdSuggestionsResponseModel> Handle(GetReorderThresholdSuggestionsRequestModel request, CancellationToken cancellationToken)
        {
            var items = await _context.InventoryItem.AsNoTracking()
                .Where(i => i.HospitalId == request.HospitalId && i.IsActive)
                .ToListAsync(cancellationToken);

            if (items.Count == 0)
                return new GetReorderThresholdSuggestionsResponseModel();

            var windowStart = DateTime.UtcNow.Date.AddDays(-TrailingWindowDays);
            var itemIds = items.Select(i => i.InventoryItemId).ToList();

            var movementsQuery = _context.InventoryMovement.AsNoTracking()
                .Where(m => m.HospitalId == request.HospitalId && m.MovementType == "ISSUE"
                         && m.MovedAt >= windowStart && itemIds.Contains(m.InventoryItemId));
            if (request.StoreId.HasValue && request.StoreId != Guid.Empty)
                movementsQuery = movementsQuery.Where(m => m.FromStoreId == request.StoreId);

            var consumptionByItem = (await movementsQuery
                    .GroupBy(m => m.InventoryItemId)
                    .Select(g => new { InventoryItemId = g.Key, TotalQty = g.Sum(x => x.Qty) })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.InventoryItemId, x => x.TotalQty);

            var buffer = request.BufferMultiplier > 0 ? request.BufferMultiplier : 1.5m;

            var result = items.Select(item =>
            {
                var trailingQty = consumptionByItem.TryGetValue(item.InventoryItemId, out var q) ? q : 0m;
                var weeklyAverage = Math.Round(trailingQty / (TrailingWindowDays / 7m), 2);
                var suggestedMin = Math.Round(weeklyAverage * buffer, 2);
                var suggestedMax = Math.Round(suggestedMin * 3, 2);

                return new ReorderThresholdSuggestionRow
                {
                    InventoryItemId = item.InventoryItemId,
                    ItemName = item.ItemName,
                    Unit = item.Unit,
                    Trailing4WeekIssuedQty = trailingQty,
                    WeeklyAverageConsumption = weeklyAverage,
                    CurrentMinStockLevel = item.MinStockLevel,
                    CurrentMaxStockLevel = item.MaxStockLevel,
                    SuggestedMinStockLevel = suggestedMin,
                    SuggestedMaxStockLevel = suggestedMax,
                    IsBelowSuggestedMin = item.CurrentStock < suggestedMin,
                };
            })
            .Where(r => r.Trailing4WeekIssuedQty > 0) // no consumption history -> nothing to suggest yet
            .OrderByDescending(r => r.IsBelowSuggestedMin)
            .ThenByDescending(r => r.WeeklyAverageConsumption)
            .ToList();

            return new GetReorderThresholdSuggestionsResponseModel { Suggestions = result };
        }
    }
}
