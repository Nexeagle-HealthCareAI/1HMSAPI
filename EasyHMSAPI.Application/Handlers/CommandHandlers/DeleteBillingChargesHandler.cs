using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeleteBillingChargesHandler : IRequestHandler<DeleteBillingChargesRequestModel, DeleteBillingChargesResponseModel>
    {
        private readonly AppDbContext _context;

        public DeleteBillingChargesHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<DeleteBillingChargesResponseModel> Handle(DeleteBillingChargesRequestModel request, CancellationToken cancellationToken)
        {

            DeleteBillingChargesResponseModel responseModel = new();
            try
            {
                var existingItem = await _context.BillingChargeCatalogs
                    .Where(x => x.ChargeItemId == request.ChargeItemId && x.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                if(existingItem == null)
                {
                    responseModel.Success = false;
                    responseModel.Message = "Billing charge item not found.";
                }
                else
                {
                    _context.BillingChargeCatalogs.Remove(existingItem);
                    await _context.SaveChangesAsync(cancellationToken);
                    responseModel.Success = true;
                    responseModel.Message = "Billing charge item deleted successfully.";
                }
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
