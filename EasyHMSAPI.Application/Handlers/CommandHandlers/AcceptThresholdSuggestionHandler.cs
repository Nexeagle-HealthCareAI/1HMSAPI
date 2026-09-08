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
        private readonly IMediator _mediator;

        public AcceptThresholdSuggestionHandler(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
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

                if (!request.RequestingStoreId.HasValue)
                {
                    return new AcceptThresholdSuggestionResponseModel { Success = true, Message = "Thresholds updated." };
                }

                // Raise a real internal stock request so "Accept" isn't a dead end -- enough to bring
                // CurrentStock up to the new MaxStockLevel. IsSystemGenerated=true lands it in DRAFT
                // (CreateIndentRequestModel's own rule) so a human still reviews/submits it rather
                // than it silently becoming a live request.
                var qtyNeeded = request.MaxStockLevel - item.CurrentStock;
                if (qtyNeeded <= 0)
                {
                    return new AcceptThresholdSuggestionResponseModel { Success = true, Message = "Thresholds updated. Current stock is already at or above the new max -- no request raised." };
                }

                var indentResponse = await _mediator.Send(new CreateIndentRequestModel
                {
                    HospitalId = request.HospitalId,
                    RequestingStoreId = request.RequestingStoreId.Value,
                    IsSystemGenerated = true,
                    Notes = $"Auto-generated from reorder threshold suggestion for {item.ItemName}.",
                    Lines = new List<IndentLineInput>
                    {
                        new() { InventoryItemId = item.InventoryItemId, Qty = qtyNeeded, Notes = "Reorder threshold suggestion" }
                    },
                    LoggedInUserName = request.LoggedInUserName,
                    LoggedInUserId = request.LoggedInUserId,
                }, cancellationToken);

                if (indentResponse.Success != true)
                {
                    return new AcceptThresholdSuggestionResponseModel
                    {
                        Success = true,
                        Message = $"Thresholds updated, but the stock request could not be raised: {indentResponse.Message}",
                    };
                }

                return new AcceptThresholdSuggestionResponseModel
                {
                    Success = true,
                    Message = $"Thresholds updated and stock request {indentResponse.IndentNumber} raised for {qtyNeeded} {item.Unit}.",
                    IndentId = indentResponse.IndentId,
                    IndentNumber = indentResponse.IndentNumber,
                };
            }
            catch (Exception)
            {
                return new AcceptThresholdSuggestionResponseModel { Success = false, Message = "Error updating thresholds." };
            }
        }
    }
}
