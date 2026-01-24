using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetInvoiceSettingsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public InvoiceSettingsDataModel? InvoiceSettings { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class InvoiceSettingsDataModel
    {
        public Guid InvoicePrintId { get; set; }
        public Guid HospitalId { get; set; }
        public int? HeaderHeight { get; set; }
        public int? FooterHeight { get; set; }
        public int? ContentLeftMargin { get; set; }
        public int? ContentRightMargin { get; set; }
        public bool? OverFlowPage { get; set; }
        public string? FontFamily { get; set; }
        public int? FontSize { get; set; }
        public string? FontWeight { get; set; }
        public string? TextColour { get; set; }
        public string? URI { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

    }
}
