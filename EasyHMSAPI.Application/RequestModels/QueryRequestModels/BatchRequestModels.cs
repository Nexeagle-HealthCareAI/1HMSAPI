using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // FEFO-sorted batch list for an item — pickers (manual issue, GRN receiving, the future
    // Narcotics dispense flow) all read from this rather than each re-implementing the expiry sort.
    [ExcludeFromCodeCoverage]
    public class GetBatchesForItemRequestModel : IRequest<GetBatchesForItemResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid? StoreId { get; set; }
        public bool ActiveOnly { get; set; } = true;
    }
}
