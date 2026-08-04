using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Toggles a doctor's manual "online now" presence flag on behalf of hospital staff.
    // HospitalId comes from the query string (bound by the controller, gated by the global
    // HospitalAccessFilter via the "hospitalId" name — same pattern as
    // UpdateDoctorPublicListingRequestModel), never the JSON body.
    [ExcludeFromCodeCoverage]
    public class UpdateDoctorOnlineStatusRequestModel : IRequest<UpdateDoctorOnlineStatusResponseModel>
    {
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public bool IsOnlineNow { get; set; }
    }
}
