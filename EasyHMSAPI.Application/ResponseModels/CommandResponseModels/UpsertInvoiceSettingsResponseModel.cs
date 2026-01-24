using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertInvoiceSettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? InvoicePrintId { get; set; }
    }
}