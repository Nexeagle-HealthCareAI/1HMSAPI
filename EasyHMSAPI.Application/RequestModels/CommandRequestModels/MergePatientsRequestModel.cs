using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>
    /// Admin action: fold a duplicate UHID into a canonical one. All linked records are repointed
    /// to the canonical patient and the duplicate registration is retired (kept for audit).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class MergePatientsRequestModel : IRequest<MergePatientsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string CanonicalPatientId { get; set; } = null!;
        public string DuplicatePatientId { get; set; } = null!;
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
