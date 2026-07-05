using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Creates a Batch with RemainingQty starting at 0 — the caller then sends
    // RecordInventoryMovementRequestModel (MovementType=RECEIVE, BatchId=this batch's id) to bring
    // RemainingQty up via the same movement mechanism everything else uses, so nothing is ever set
    // outside that single audited path.
    [ExcludeFromCodeCoverage]
    public class CreateBatchRequestModel : IRequest<CreateBatchResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid InventoryItemId { get; set; }
        public Guid StoreId { get; set; }
        public string BatchNumber { get; set; } = null!;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal ReceivedQty { get; set; }
        public Guid? VendorId { get; set; }
        public Guid? GrnLineId { get; set; }
    }
}
