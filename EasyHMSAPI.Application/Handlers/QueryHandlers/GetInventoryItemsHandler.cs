using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetInventoryItemsHandler : IRequestHandler<GetInventoryItemsRequestModel, GetInventoryItemsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetInventoryItemsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetInventoryItemsResponseModel> Handle(GetInventoryItemsRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.InventoryItem.Where(i => i.HospitalId == request.HospitalId);

            if (request.ActiveOnly)
                query = query.Where(i => i.IsActive);

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                var category = request.Category.Trim().ToUpperInvariant();
                query = query.Where(i => i.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                query = query.Where(i => i.ItemName.Contains(search) || i.ItemCode.Contains(search));
            }

            var items = await query
                .OrderBy(i => i.ItemName)
                .Select(i => new InventoryItemDataModel
                {
                    InventoryItemId = i.InventoryItemId,
                    ItemCode = i.ItemCode,
                    ItemName = i.ItemName,
                    GenericName = i.GenericName,
                    Manufacturer = i.Manufacturer,
                    SaltCompositionId = i.SaltCompositionId,
                    Category = i.Category,
                    Unit = i.Unit,
                    DefaultRate = i.DefaultRate,
                    HsnSacCode = i.HsnSacCode,
                    GstSlabPercent = i.GstSlabPercent,
                    IsTaxable = i.IsTaxable,
                    ChargeId = i.ChargeId,
                    CurrentStock = i.CurrentStock,
                    MinStockLevel = i.MinStockLevel,
                    StoreLocation = i.StoreLocation,
                    ScheduleClass = i.ScheduleClass,
                    IsLasa = i.IsLasa,
                    IsHighAlert = i.IsHighAlert,
                    StorageCondition = i.StorageCondition,
                    ReorderQty = i.ReorderQty,
                    MaxStockLevel = i.MaxStockLevel,
                    IsActive = i.IsActive,
                })
                .ToListAsync(cancellationToken);

            return new GetInventoryItemsResponseModel { Items = items };
        }
    }
}
