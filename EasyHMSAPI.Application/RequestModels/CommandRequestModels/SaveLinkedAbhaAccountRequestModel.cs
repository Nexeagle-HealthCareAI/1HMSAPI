using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Persists an ABHA profile fetched via the Link-Existing (Mobile/Aadhaar-OTP login)
    /// wizard once the user confirms it — the login step itself doesn't write to the DB.</summary>
    [ExcludeFromCodeCoverage]
    public class SaveLinkedAbhaAccountRequestModel : IRequest<SaveAbhaAccountResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string AbhaNumber { get; set; } = string.Empty;
        public string? AbhaAddress { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
