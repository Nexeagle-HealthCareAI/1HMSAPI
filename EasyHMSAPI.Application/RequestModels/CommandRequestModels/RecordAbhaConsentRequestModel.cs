using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Records that the patient (or their guardian) was shown the consent wording and agreed, before an ABHA enrolment OTP is requested.</summary>
    [ExcludeFromCodeCoverage]
    public class RecordAbhaConsentRequestModel : IRequest<RecordAbhaConsentResponseModel>
    {
        public Guid HospitalId { get; set; }
        /// <summary>The wording version the screen displayed; must equal the server's current version, so a stale screen cannot record consent to old text.</summary>
        public string? ConsentVersion { get; set; }
        /// <summary>PATIENT or GUARDIAN.</summary>
        public string? GivenBy { get; set; }
        public string? SubjectName { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
