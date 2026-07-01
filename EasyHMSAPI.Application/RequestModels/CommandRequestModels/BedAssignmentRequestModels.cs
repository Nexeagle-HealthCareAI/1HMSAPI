using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Assigns a bed to an admission that doesn't currently have one. Fails if the admission already
    // has an ACTIVE bed (use TransferBed) or if the bed is already taken (DB unique-index backstop).
    [ExcludeFromCodeCoverage]
    public class AssignBedRequestModel : IRequest<AssignBedResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid BedId { get; set; }
    }

    // Releases the admission's current ACTIVE bed assignment (e.g. on discharge).
    [ExcludeFromCodeCoverage]
    public class ReleaseBedRequestModel : IRequest<ReleaseBedResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }
        public string? Notes { get; set; }
    }

    // Moves an admission from its current bed to a new one: releases the old assignment and creates
    // a new one atomically. Re-rating for the new bed's rate (timestamped charge segments) is Phase 2.
    [ExcludeFromCodeCoverage]
    public class TransferBedRequestModel : IRequest<TransferBedResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid NewBedId { get; set; }
        public string? Notes { get; set; }
    }
}
