using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Cancels the patient's entire latest encounter and voids every charge on it -- NOT a
    // single-charge cancel despite the similarly-named single-charge delete flow elsewhere.
    // Renamed from CancelChargeEventRequestModel, which had no ChargeEventId field at all and
    // was a misleading name for what this actually does.
    [ExcludeFromCodeCoverage]
    public class CancelEncounterChargesRequestModel : IRequest<CancelEncounterChargesResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public string? CancelReason { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
