using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteInvoiceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int ChargesVoided { get; set; }
    }
}
