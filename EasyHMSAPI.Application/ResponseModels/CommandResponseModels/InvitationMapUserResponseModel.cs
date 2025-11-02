using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class InvitationMapUserResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid InvitationId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }
        public bool CreatedHospitalUserLink { get; set; }
        public string? InvitationStatus { get; set; }
    }
}
