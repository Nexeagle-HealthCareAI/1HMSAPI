using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class PharmacyReturnLineInput
    {
        public Guid ChargeEventId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid BatchId { get; set; }
        public decimal ReturnedQty { get; set; }
        public decimal UnitPrice { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CreatePharmacyReturnRequestModel : IRequest<CreatePharmacyReturnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string InvoiceNo { get; set; } = null!;
        public string? RefundMode { get; set; }
        public string? Notes { get; set; }
        public List<PharmacyReturnLineInput> Lines { get; set; } = new();

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
