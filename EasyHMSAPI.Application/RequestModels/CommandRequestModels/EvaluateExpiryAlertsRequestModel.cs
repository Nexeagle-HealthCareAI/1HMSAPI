using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Batch-expiry counterpart to EvaluateAlertsRequestModel — raises EXPIRY_90/60/30 Alert rows
    // (and an SMS to the hospital's main contact) instead of scanning admissions. Invoked on-demand
    // via AlertsController and once daily by ExpiryAlertBackgroundService.
    [ExcludeFromCodeCoverage]
    public class EvaluateExpiryAlertsRequestModel : IRequest<EvaluateExpiryAlertsResponseModel>
    {
        public Guid HospitalId { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
