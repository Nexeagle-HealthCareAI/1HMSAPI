using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertBillingChangesRequestModel : IRequest<UpsertBillingChangesResponseModel>
    {
        public Guid? ChargeItemId { get; set; }
        public Guid HospitalId { get; set; }
        public string? DisplayName { get; set; }
        public string? VisitType { get; set; }
        public decimal DefaultRate { get; set; }
        public decimal? DefaultDiscountPercent { get; set; }
        public decimal DefaultQty { get; set; }
        [JsonIgnore]
        public DateTime CurrentDateTime { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
