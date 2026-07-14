using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class InsertDeviceRequestModel : IRequest<InsertDeviceResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public string DeviceType { get; set; } = null!;
        public string? InsertionSite { get; set; }
        public string? Indication { get; set; }
        public string InsertedByDoctorName { get; set; } = null!;
        public string? Notes { get; set; }
    }

    // Mirrors RestraintOrderCommandHandlers' Release lifecycle shape.
    [ExcludeFromCodeCoverage]
    public class RemoveDeviceRequestModel : IRequest<RemoveDeviceResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid DeviceAssignmentId { get; set; }
        public string? RemovalReason { get; set; }
    }
}
