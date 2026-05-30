using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class EvaluateAlertsRequestModel : IRequest<EvaluateAlertsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public decimal? DepositLowThresholdAmount { get; set; }
        public int? ConsentPendingGraceHours { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
