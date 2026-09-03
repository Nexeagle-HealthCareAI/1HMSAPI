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

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
