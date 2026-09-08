using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class VendorReturnLineInput
    {
        public Guid BatchId { get; set; }
        public decimal Qty { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CreateVendorReturnRequestModel : IRequest<CreateVendorReturnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid VendorId { get; set; }
        public string? Notes { get; set; }
        public List<VendorReturnLineInput> Lines { get; set; } = new();

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
