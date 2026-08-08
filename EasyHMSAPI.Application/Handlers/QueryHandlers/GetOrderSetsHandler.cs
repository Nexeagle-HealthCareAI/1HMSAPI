using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetOrderSetsHandler : IRequestHandler<GetOrderSetsRequestModel, GetOrderSetsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetOrderSetsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetOrderSetsResponseModel> Handle(GetOrderSetsRequestModel request, CancellationToken cancellationToken)
        {
            GetOrderSetsResponseModel response = new() { Success = false };
            try
            {
                var query = _context.OrderSets.AsNoTracking()
                    .Where(o => o.HospitalId == request.HospitalId);

                if (!request.IncludeInactive)
                    query = query.Where(o => o.IsActive);
                if (!string.IsNullOrWhiteSpace(request.Category))
                {
                    var category = request.Category.Trim().ToUpperInvariant();
                    query = query.Where(o => o.Category == category);
                }

                var orderSets = await query
                    .OrderBy(o => o.Name)
                    .ToListAsync(cancellationToken);

                response.OrderSets = orderSets.Select(o => new OrderSetDataModel
                {
                    OrderSetId = o.OrderSetId,
                    Name = o.Name,
                    Category = o.Category,
                    Lines = string.IsNullOrWhiteSpace(o.TemplateLinesJson)
                        ? new List<OrderSetLineDataModel>()
                        : JsonSerializer.Deserialize<List<OrderSetLineDataModel>>(o.TemplateLinesJson) ?? new List<OrderSetLineDataModel>(),
                    IsActive = o.IsActive,
                    UpdatedAt = o.UpdatedAt,
                    UpdatedBy = o.UpdatedBy,
                }).ToList();
                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
