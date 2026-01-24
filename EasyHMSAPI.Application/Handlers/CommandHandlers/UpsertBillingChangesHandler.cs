using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertBillingChangesHandler : IRequestHandler<UpsertBillingChangesRequestModel, UpsertBillingChangesResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertBillingChangesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertBillingChangesResponseModel> Handle(UpsertBillingChangesRequestModel request, CancellationToken cancellationToken)
        {
            UpsertBillingChangesResponseModel response = new(); 
            try
            {
                var existingHospital = await _context.Hospitals.AnyAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
                if(existingHospital)
                {
                    if (request.ChargeItemId == Guid.Empty)
                    {
                        BillingChargeCatalog billingChargeCatalog = new()
                        {
                            ChargeItemId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            DisplayName = request.DisplayName,
                            VisitType = !string.IsNullOrEmpty(request.VisitType) ? request.VisitType.Trim().ToUpper() : string.Empty,
                            DefaultRate = request.DefaultRate,
                            DefaultDiscountPercent = request.DefaultDiscountPercent,
                            DefaultQty = request.DefaultQty,
                            UpdatedAt = request.CurrentDateTime,
                            UpdatedBy = request.LoggedInUserName
                        };
                        _context.BillingChargeCatalogs.Add(billingChargeCatalog);
                        await _context.SaveChangesAsync(cancellationToken);

                        response.Success = true;
                        response.Message = "Billing change inserted successfully.";
                    }
                    else
                    {
                        var existingItem = await _context.BillingChargeCatalogs
                            .Where(x => x.ChargeItemId == request.ChargeItemId)
                            .FirstOrDefaultAsync(cancellationToken);
                        if(existingItem is not null)
                        {
                            if (!string.IsNullOrEmpty(request.DisplayName)) existingItem.DisplayName = request.DisplayName;
                            if (!string.IsNullOrEmpty(request.VisitType)) existingItem.VisitType = request.VisitType.Trim().ToUpper();
                            existingItem.DefaultRate = request.DefaultRate;
                            existingItem.DefaultDiscountPercent = request.DefaultDiscountPercent;
                            existingItem.DefaultQty = request.DefaultQty;
                            existingItem.UpdatedAt = request.CurrentDateTime;
                            existingItem.UpdatedBy = request.LoggedInUserName;

                            await _context.SaveChangesAsync(cancellationToken);
                            response.Success = true;
                            response.Message = "Billing change updated successfully.";
                        }
                        else
                        {
                            response.Success = false;
                            response.Message = "Billing charge item does not exist.";
                        }
                        
                    }
                }
                else
                {
                    response.Success = false;
                    response.Message = "Hospital does not exist.";
                    return response;

                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }

    }
}
