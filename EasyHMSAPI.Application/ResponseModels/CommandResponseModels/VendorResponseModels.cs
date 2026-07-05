using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertVendorResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid VendorId { get; set; }
    }
}
