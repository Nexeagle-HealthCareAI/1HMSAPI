using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateVendorReturnResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid VendorReturnId { get; set; }
        public string? ReturnNoteNo { get; set; }
        public decimal TotalQty { get; set; }
        public decimal TotalValue { get; set; }
    }
}
