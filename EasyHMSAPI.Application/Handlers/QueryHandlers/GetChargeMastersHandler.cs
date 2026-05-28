using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetChargeMastersHandler : IRequestHandler<GetChargeMastersRequestModel, GetChargeMastersResponseModel>
    {
        private readonly AppDbContext _context;

        public GetChargeMastersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetChargeMastersResponseModel> Handle(GetChargeMastersRequestModel request, CancellationToken cancellationToken)
        {
            var baseQuery = _context.ChargeMaster.Where(x => x.HospitalId == request.HospitalId);

            var totalCount = await baseQuery.CountAsync(cancellationToken);

            var items = await baseQuery
                .OrderBy(c => c.SortOrder)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(c => new ChargeMastersDataModel
                {
                    ChargeId = c.ChargeId,
                    ChargeCode = c.ChargeCode,
                    DisplayName = c.DisplayName,
                    CategoryCode = c.CategoryCode,
                    SubCategoryCode = c.SubCategoryCode,
                    AppliesTo = c.AppliesTo,
                    DefaultRate = c.DefaultRate,
                    DefaultQty = c.DefaultQty,
                    MaxDiscountPercent = c.MaxDiscountPercent,
                    IncentiveAmount = c.IncentiveAmount,
                    HsnSacCode = c.HsnSacCode,
                    IsTaxable = c.IsTaxable,
                    GstSlabPercent = c.GstSlabPercent,
                    TaxInclusive = c.TaxInclusive,
                    IsActive = c.IsActive,
                    SortOrder = c.SortOrder,
                    UpdatedAt = c.UpdatedAt,
                    UpdatedBy = c.UpdatedBy,
                    Notes = c.Notes
                })
                .ToListAsync(cancellationToken);

            return new GetChargeMastersResponseModel
            {
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                Items = items
            };
        }
    }
}
