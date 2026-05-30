using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateChargeEventRequestModel : IRequest<CreateChargeEventResponseModel>
    {
        public string? PatientId { get; set; }
        public Guid HospitalId { get; set; }
        public string? EncounterType { get; set; }
        // Specific appointment to bill against. When null, the patient's latest appointment is used.
        public Guid? AppointmentId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
