using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class AcceptThresholdSuggestionHandler : IRequestHandler<AcceptThresholdSuggestionRequestModel, AcceptThresholdSuggestionResponseModel>
    {
        private readonly AppDbContext _context;

        public AcceptThresholdSuggestionHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AcceptThresholdSuggestionResponseModel> Handle(AcceptThresholdSuggestionRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.InventoryItemId == Guid.Empty)
                    return new AcceptThresholdSuggestionResponseModel { Success = false, Message = "HospitalId and InventoryItemId are required." };
                if (request.MinStockLevel < 0 || request.MaxStockLevel < request.MinStockLevel)
                    return new AcceptThresholdSuggestionResponseModel { Success = false, Message = "MaxStockLevel must be greater than or equal to MinStockLevel." };

                var item = await _context.InventoryItem.FirstOrDefaultAsync(
                    i => i.InventoryItemId == request.InventoryItemId && i.HospitalId == request.HospitalId, cancellationToken);
                if (item == null)
                    return new AcceptThresholdSuggestionResponseModel { Success = false, Message = "Inventory item not found." };

                item.MinStockLevel = request.MinStockLevel;
                item.MaxStockLevel = request.MaxStockLevel;
                item.UpdatedAt = DateTime.UtcNow;
                item.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new AcceptThresholdSuggestionResponseModel { Success = true, Message = "Thresholds updated." };
            }
            catch (Exception)
            {
                return new AcceptThresholdSuggestionResponseModel { Success = false, Message = "Error updating thresholds." };
            }
        }
    }
}
