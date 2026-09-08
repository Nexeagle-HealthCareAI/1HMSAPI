using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Near-expiry report — every ACTIVE batch with remaining stock, bucketed by days-to-expiry.
    // Filters are all optional; omit to see the whole hospital.
    [ExcludeFromCodeCoverage]
    public class GetNearExpiryReportRequestModel : IRequest<GetNearExpiryReportResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? StoreId { get; set; }
        public Guid? VendorId { get; set; }
        // Green/Yellow/Orange/Red — omit for all buckets. Red batches are locked from POS already
        // (FefoBatchAllocationService); this report is what surfaces them to a store manager.
        public string? Bucket { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetDrugScheduleRegisterRequestModel : IRequest<GetDrugScheduleRegisterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string? ScheduleClass { get; set; }
    }
}
