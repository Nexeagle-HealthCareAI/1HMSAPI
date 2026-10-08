using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertConsultantFeeConfigRequestModel : IRequest<UpsertConsultantFeeConfigResponseModel>
    {
        [JsonIgnore]
        public Guid HrEmployeeId { get; set; }
        public DateOnly EffectiveFrom { get; set; }
        public decimal MonthlyRetainer { get; set; }
        public decimal OpdSharePercent { get; set; }
        public decimal IpdVisitFee { get; set; }
        public decimal AdminSurcharge { get; set; }
        public string? SurgeryShareConfigJson { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
