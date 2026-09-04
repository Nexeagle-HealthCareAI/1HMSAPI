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

    // Keyboard-wedge scanner lookup: a scan types the barcode into the POS search box on Enter —
    // this resolves it straight to the item + specific batch, skipping name search entirely.
    [ExcludeFromCodeCoverage]
    public class GetBatchByBarcodeRequestModel : IRequest<GetBatchByBarcodeResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? StoreId { get; set; }
        public string BarcodeValue { get; set; } = null!;
    }

    // Hospital-wide, non-expiry-filtered batch list — the "view/verify all current stock" screen,
    // backing the Batches tab. Search matches item name, item code, or batch number.
    [ExcludeFromCodeCoverage]
    public class GetAllBatchesRequestModel : IRequest<GetAllBatchesResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? StoreId { get; set; }
        public string? Search { get; set; }
        public bool ActiveOnly { get; set; } = true;
    }
}
