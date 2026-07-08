using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetStoresHandler : IRequestHandler<GetStoresRequestModel, GetStoresResponseModel>
    {
        private readonly AppDbContext _context;

        public GetStoresHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetStoresResponseModel> Handle(GetStoresRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.Store.Where(s => s.HospitalId == request.HospitalId);
            if (!request.IncludeInactive)
                query = query.Where(s => s.IsActive);

            var stores = await query.ToListAsync(cancellationToken);
            var byId = stores.ToDictionary(s => s.StoreId, s => s.StoreName);

            var result = stores
                .OrderBy(s => s.StoreCode)
                .Select(s => new StoreDataModel
                {
                    StoreId = s.StoreId,
                    StoreCode = s.StoreCode,
                    StoreName = s.StoreName,
                    StoreType = s.StoreType,
                    AssignedBoard = s.AssignedBoard,
                    ParentStoreId = s.ParentStoreId,
                    ParentStoreName = s.ParentStoreId.HasValue && byId.TryGetValue(s.ParentStoreId.Value, out var name) ? name : null,
                    MinTempCelsius = s.MinTempCelsius,
                    MaxTempCelsius = s.MaxTempCelsius,
                    IsActive = s.IsActive,
                })
                .ToList();

            return new GetStoresResponseModel { Stores = result };
        }
    }
}
