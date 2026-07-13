using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Toggles whether one doctor shows on the platform-wide public directory. HospitalId comes
    // from the query string (bound by the controller, gated by the global HospitalAccessFilter via
    // the "hospitalId" name — same pattern as UpsertDoctorFeeRequestModel), never the JSON body.
    [ExcludeFromCodeCoverage]
    public class UpdateDoctorPublicListingRequestModel : IRequest<UpdateDoctorPublicListingResponseModel>
    {
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public bool IsPubliclyListed { get; set; }
    }
}
