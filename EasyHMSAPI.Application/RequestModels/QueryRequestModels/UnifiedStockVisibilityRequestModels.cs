using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Combines InventoryItem/StockLevel, BloodBag, and InstrumentSet into one "everything, every
    // store" view for the Inventory Board's unified tab — purely additive, reads only, doesn't
    // touch either module's own bespoke business logic.
    [ExcludeFromCodeCoverage]
    public class GetUnifiedStockVisibilityRequestModel : IRequest<GetUnifiedStockVisibilityResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
