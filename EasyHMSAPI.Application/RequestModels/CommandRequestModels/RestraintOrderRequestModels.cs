using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class StartRestraintRequestModel : IRequest<StartRestraintResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public string RestraintType { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public Guid? OrderedByDoctorId { get; set; }
        public string OrderedByDoctorName { get; set; } = null!;
        public int MonitoringIntervalMins { get; set; } = 30;
        public bool FamilyNotified { get; set; }
        public string? FamilyNotificationNotes { get; set; }
        public Guid? RelatedConsentRecordId { get; set; }
        public string? Notes { get; set; }
    }

    // Mirrors BedAssignmentCommandHandlers' ReleaseBed lifecycle shape.
    [ExcludeFromCodeCoverage]
    public class ReleaseRestraintRequestModel : IRequest<ReleaseRestraintResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid RestraintOrderId { get; set; }
        public string? ReleaseReason { get; set; }
    }
}
