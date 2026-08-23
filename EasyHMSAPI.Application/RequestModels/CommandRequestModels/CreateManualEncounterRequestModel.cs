using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Creates a billing encounter for a registered patient WITHOUT requiring an appointment —
    /// used for manual billing (e.g. IPD). Unlike CreateChargeEvent it doesn't link to an
    /// appointment or auto-post a consult charge.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class CreateManualEncounterRequestModel : IRequest<CreateManualEncounterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public string? EncounterType { get; set; }   // IPD / OPD / ER / LAB / PHARMACY
        public Guid? DoctorId { get; set; }            // optional attending doctor

        // Optional visit-date override -- every charge/invoice on this visit will silently use
        // this date instead of "now". Null (the default) means unchanged, real-time behavior.
        public DateTime? ServiceDate { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
