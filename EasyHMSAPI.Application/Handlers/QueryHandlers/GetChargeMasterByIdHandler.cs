using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetChargeMasterByIdHandler : IRequestHandler<GetChargeMasterByIdRequestModel, GetChargeMasterByIdResponseModel>
    {
        private readonly AppDbContext _context;

        public GetChargeMasterByIdHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetChargeMasterByIdResponseModel> Handle(GetChargeMasterByIdRequestModel request, CancellationToken cancellationToken)
        {
            var charge = await _context.ChargeMaster
                .Where(x => x.ChargeId == request.ChargeId && x.HospitalId == request.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);

            if (charge == null)
            {
                throw new KeyNotFoundException("Charge not found");
            }

            return new GetChargeMasterByIdResponseModel
            {
                ChargeId = charge.ChargeId,
                ChargeCode = charge.ChargeCode,
                DisplayName = charge.DisplayName,
                CategoryCode = charge.CategoryCode,
                SubCategoryCode = charge.SubCategoryCode,
                AppliesTo = charge.AppliesTo,
                DefaultRate = charge.DefaultRate,
                DefaultQty = charge.DefaultQty,
                MaxDiscountPercent = charge.MaxDiscountPercent,
                IncentiveAmount = charge.IncentiveAmount,
                HsnSacCode = charge.HsnSacCode,
                IsTaxable = charge.IsTaxable,
                GstSlabPercent = charge.GstSlabPercent,
                TaxInclusive = charge.TaxInclusive,
                IsActive = charge.IsActive,
                IsIRDAIPayable = charge.IsIRDAIPayable,
                SortOrder = charge.SortOrder,
                UpdatedAt = charge.UpdatedAt,
                UpdatedBy = charge.UpdatedBy,
                Notes = charge.Notes,
            };
        }
    }
}
