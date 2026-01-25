using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertInvoiceSettingsRequestModel : IRequest<UpsertInvoiceSettingsResponseModel>
    {
        public  Guid HospitalId { get; set; }
        public Guid? InvoicePrintId { get; set; }
        public int? FeaderHeight { get; set; }
        public int? FooterHeight { get; set; }
        public int? ContentLeftMargin { get; set; }
        public int? ContentRightMargin { get; set; }
        public bool? OverFlowPage { get; set; }
        public string? FontFamily { get; set; }
        public int? FontSize { get; set; }
        public string? FontWeight { get; set; }
        public string? TextColour { get; set; }
        [JsonIgnore]
        public DateTime CurrentDateTime { get; set; }
        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
