using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Receive a new blood bag into the pool (already-collected unit — no donor eligibility
    // screening workflow this phase, DonorRef is free text).
    [ExcludeFromCodeCoverage]
    public class ReceiveBloodBagRequestModel : IRequest<ReceiveBloodBagResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public string BagNumber { get; set; } = null!;
        public string Component { get; set; } = null!;
        public string BloodGroup { get; set; } = null!;
        public decimal VolumeMl { get; set; }
        public string? DonorRef { get; set; }
        public DateTime CollectedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? StorageLocation { get; set; }
        public Guid? ChargeId { get; set; }
        public decimal? UnitRate { get; set; }
    }

    // Cross-match: reserves a bag for a specific admission/patient and records the result.
    [ExcludeFromCodeCoverage]
    public class ReserveBloodBagRequestModel : IRequest<ReserveBloodBagResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid BloodBagId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? CrossmatchResult { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DiscardBloodBagRequestModel : IRequest<DiscardBloodBagResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid BloodBagId { get; set; }
        public string DiscardReason { get; set; } = null!;
    }

    // Records a transfusion against a reserved (or available) bag. Fires a billing charge event
    // if ChargeId is supplied and the admission's EncounterId is non-null — same guard CPOE uses.
    [ExcludeFromCodeCoverage]
    public class RecordTransfusionRequestModel : IRequest<RecordTransfusionResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid BloodBagId { get; set; }
        public Guid AdmissionId { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public decimal VolumeGivenMl { get; set; }

        public string? VitalsBefore { get; set; }
        public string? VitalsAfter { get; set; }

        public string Reaction { get; set; } = "NONE";
        public string? ReactionNotes { get; set; }

        public string WitnessName { get; set; } = null!;
        public Guid? WitnessUserId { get; set; }

        public string? Notes { get; set; }

        // Optional bill-this-transfusion — omit to skip billing entirely.
        public Guid? ChargeId { get; set; }
        public decimal? Rate { get; set; }
    }
}
