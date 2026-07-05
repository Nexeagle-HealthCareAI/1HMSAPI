using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateInstrumentSetRequestModel : IRequest<CreateInstrumentSetResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public string SetCode { get; set; } = null!;
        public string SetName { get; set; } = null!;
        public string? Category { get; set; }
        public string? ItemComposition { get; set; }
        public string? CurrentLocation { get; set; }
    }

    // Unified movement handler — one request shape for the full issue-to-OT -> return -> wash ->
    // pack -> sterilize -> store loop (mirrors RecordMedicationAdministrationRequestModel's
    // single-handler-many-actions shape). Updates InstrumentSet.CurrentStatus/CurrentLocation from
    // a fixed MovementType->Status mapping.
    [ExcludeFromCodeCoverage]
    public class RecordInstrumentSetMovementRequestModel : IRequest<RecordInstrumentSetMovementResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid InstrumentSetId { get; set; }
        public string MovementType { get; set; } = null!;
        public Guid? SurgeryCaseId { get; set; }
        public string? Location { get; set; }
        // Unified Store reference (INV-10) — optional, sets InstrumentSet.StoreId alongside Location.
        public Guid? StoreId { get; set; }
        public string? Notes { get; set; }
    }

    // Creates a sterilization cycle covering one or more instrument sets. On completion (Ended*
    // supplied with a BiologicalIndicatorResult), linked sets flip to STERILE (PASS) or
    // QUARANTINED (FAIL) — never silently back to AVAILABLE on a FAIL result.
    [ExcludeFromCodeCoverage]
    public class RecordSterilizationCycleRequestModel : IRequest<RecordSterilizationCycleResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public string CycleNumber { get; set; } = null!;
        public string? AutoclaveLabel { get; set; }
        public string CycleType { get; set; } = null!;
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string BiologicalIndicatorResult { get; set; } = "PENDING";
        public string? ChemicalIndicatorResult { get; set; }
        public string? Notes { get; set; }
        public List<Guid> InstrumentSetIds { get; set; } = new();
    }
}
