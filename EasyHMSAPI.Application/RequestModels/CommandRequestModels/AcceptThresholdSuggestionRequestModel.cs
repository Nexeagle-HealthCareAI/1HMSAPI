using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class AcceptThresholdSuggestionRequestModel : IRequest<AcceptThresholdSuggestionResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }
        public decimal MinStockLevel { get; set; }
        public decimal MaxStockLevel { get; set; }

        // Optional -- when supplied, also raises a real system-generated Indent (internal stock
        // request) for this store, requesting enough to bring CurrentStock up to MaxStockLevel.
        // Without this, "Accept" only ever adjusted the threshold numbers themselves and never
        // actually requested any stock -- a dead-end suggestion that looked actionable but wasn't.
        public Guid? RequestingStoreId { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
