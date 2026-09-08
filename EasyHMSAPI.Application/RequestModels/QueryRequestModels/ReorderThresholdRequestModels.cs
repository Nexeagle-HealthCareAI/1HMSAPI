using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Suggests Min/Max stock levels from trailing consumption — never writes anything; the store
    // manager reviews and accepts per item via AcceptThresholdSuggestion.
    [ExcludeFromCodeCoverage]
    public class GetReorderThresholdSuggestionsRequestModel : IRequest<GetReorderThresholdSuggestionsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? StoreId { get; set; }
        // Multiplier applied to the trailing weekly-average consumption to get the suggested Min
        // (safety buffer against a slow week); Max = suggested Min x 3 (roughly a month's cover).
        // Defaults chosen to be a starting point, not a policy — always overridable per call.
        public decimal BufferMultiplier { get; set; } = 1.5m;
    }
}
