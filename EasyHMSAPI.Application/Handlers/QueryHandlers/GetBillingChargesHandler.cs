using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetBillingChargesHandler : IRequestHandler<GetBillingChargesRequestModel, GetBillingChargesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBillingChargesHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<GetBillingChargesResponseModel> Handle(GetBillingChargesRequestModel request, CancellationToken cancellationToken)
        {
            GetBillingChargesResponseModel responseModel = new(); 
            try
            {
                var existingItem = await _context.BillingChargeCatalogs
                    .Where(x => x.HospitalId == request.HospitalId)
                    .Select(x => new BillingChargeItemDataModel
                    {
                        ChargeItemId = x.ChargeItemId,
                        HospitalId = x.HospitalId,
                        DisplayName = x.DisplayName,
                        VisitType = x.VisitType,
                        DefaultRate = x.DefaultRate,
                        DefaultDiscountPercent = x.DefaultDiscountPercent,
                        DefaultQty = x.DefaultQty,
                        UpdatedAt = x.UpdatedAt,
                        UpdatedBy = x.UpdatedBy
                    })
                    .ToListAsync(cancellationToken);

                responseModel.Success = true;
                responseModel.Message = "Billing charges retrieved successfully.";
                responseModel.Data = existingItem;

            }
            catch (Exception ex)
            {
                responseModel.Success = false;
                responseModel.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return responseModel;
        }
    }
}
