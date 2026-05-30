using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertBillingPolicyRequestModel : IRequest<UpsertBillingPolicyResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? LabPathTrigger { get; set; }
        public string? LabRadTrigger { get; set; }
        public string? PharmacyIpdTrigger { get; set; }
        public string? OpdConsultTrigger { get; set; }
        public string? IpdBedChargeMode { get; set; }

        // GST
        public string? SupplierGstin { get; set; }
        public string? PlaceOfSupplyStateCode { get; set; }
        public bool DefaultPriceIsTaxInclusive { get; set; }
        public string? TaxRoundingMode { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public NumberSeriesUpdateModel? NumberSeries { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class NumberSeriesUpdateModel
    {
        public NumberSeriesItemUpdateModel? Invoice { get; set; }
        public NumberSeriesItemUpdateModel? Receipt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class NumberSeriesItemUpdateModel
    {
        [MaxLength(50)]
        public string? Prefix { get; set; }
        [MaxLength(20)]
        public string? YearFormat { get; set; }
        [MaxLength(5)]
        public string? Separator { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "PadLength must be at least 1")]
        public int PadLength { get; set; }
        public bool IsActive { get; set; }
    }
}
