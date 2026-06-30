using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertChargeMasterHandler : IRequestHandler<UpsertChargeMasterRequestModel, UpsertChargeMasterResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertChargeMasterHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertChargeMasterResponseModel> Handle(UpsertChargeMasterRequestModel request, CancellationToken cancellationToken)
        {
            if (request.ChargeId != null && request.ChargeId != Guid.Empty)
            {
                var existingCharge = await _context.ChargeMaster
                    .FirstOrDefaultAsync(x => x.ChargeId == request.ChargeId && x.HospitalId == request.HospitalId, cancellationToken);

                if (existingCharge == null)
                    throw new Exception("Charge not found for update");

                existingCharge.ChargeCode = request.ChargeCode;
                existingCharge.DisplayName = request.DisplayName;
                existingCharge.CategoryCode = request.CategoryCode;
                existingCharge.SubCategoryCode = request.SubCategoryCode;
                existingCharge.AppliesTo = request.AppliesTo;
                existingCharge.DefaultRate = request.DefaultRate;
                existingCharge.DefaultQty = request.DefaultQty;
                existingCharge.MaxDiscountPercent = request.MaxDiscountPercent;
                existingCharge.IncentiveAmount = request.IncentiveAmount;
                existingCharge.HsnSacCode = request.HsnSacCode;
                existingCharge.IsTaxable = request.IsTaxable;
                existingCharge.GstSlabPercent = request.GstSlabPercent;
                existingCharge.TaxInclusive = request.TaxInclusive;
                existingCharge.IsActive = request.IsActive;
                existingCharge.SortOrder = request.SortOrder;
                existingCharge.Notes = request.Notes;
                existingCharge.UpdatedAt = DateTime.UtcNow;
                existingCharge.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertChargeMasterResponseModel
                {
                    ChargeId = existingCharge.ChargeId,
                    ChargeCode = existingCharge.ChargeCode
                };
            }

            var charge = new ChargeMaster
            {
                ChargeId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                ChargeCode = request.ChargeCode,
                DisplayName = request.DisplayName,
                CategoryCode = request.CategoryCode,
                SubCategoryCode = request.SubCategoryCode,
                AppliesTo = request.AppliesTo,
                DefaultRate = request.DefaultRate,
                DefaultQty = request.DefaultQty,
                MaxDiscountPercent = request.MaxDiscountPercent,
                IncentiveAmount = request.IncentiveAmount,
                HsnSacCode = request.HsnSacCode,
                IsTaxable = request.IsTaxable,
                GstSlabPercent = request.GstSlabPercent,
                TaxInclusive = request.TaxInclusive,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.LoggedInUserName,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = request.LoggedInUserName
            };

            _context.ChargeMaster.Add(charge);
            await _context.SaveChangesAsync(cancellationToken);

            return new UpsertChargeMasterResponseModel
            {
                ChargeId = charge.ChargeId,
                ChargeCode = charge.ChargeCode
            };
        }
    }
}
