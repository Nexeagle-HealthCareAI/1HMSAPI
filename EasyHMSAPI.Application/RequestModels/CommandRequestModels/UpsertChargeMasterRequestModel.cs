using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertChargeMasterRequestModel : IRequest<UpsertChargeMasterResponseModel>
    {
        public Guid? ChargeId { get; set; }
        public Guid HospitalId { get; set; }
        public string? ChargeCode { get; set; }
        public string? DisplayName { get; set; }
        public string? CategoryCode { get; set; }
        public string? SubCategoryCode { get; set; }
        public string? AppliesTo { get; set; }
        public decimal DefaultRate { get; set; }
        public decimal DefaultQty { get; set; }
        public decimal? MaxDiscountPercent { get; set; }
        public decimal? IncentiveAmount { get; set; }
        public string? HsnSacCode { get; set; }
        public bool IsTaxable { get; set; }
        public decimal? GstSlabPercent { get; set; }
        public bool TaxInclusive { get; set; }
        public bool IsActive { get; set; }
        // Nullable so "omitted" (new item, no opinion yet) can default to payable=true server-side
        // without a client that doesn't send this field silently flipping an existing item to
        // non-payable on update.
        public bool? IsIRDAIPayable { get; set; }
        public int SortOrder { get; set; }
        public string? Notes { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
