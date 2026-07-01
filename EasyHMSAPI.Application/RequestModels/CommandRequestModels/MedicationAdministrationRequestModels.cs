using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Records one nurse action (ADMINISTERED/HELD/REFUSED/PATIENT_NOT_AVAILABLE) against a
    // computed MAR dose slot. ScheduledFor is the slot's computed time as shown on the grid the
    // nurse is acting from (echoed back, not re-derived server-side, so the write matches exactly
    // what the nurse saw) — the handler still re-validates it against a freshly computed schedule
    // before accepting it, so a stale/tampered client can't post an arbitrary time. One shape
    // covers all 4 actions, matching how PlaceClinicalOrderRequestModel covers every OrderType.
    [ExcludeFromCodeCoverage]
    public class RecordMedicationAdministrationRequestModel : IRequest<RecordMedicationAdministrationResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid OrderLineId { get; set; }
        public DateTime ScheduledFor { get; set; }   // the slot being acted on (UTC)
        public string ActionStatus { get; set; } = null!;   // ADMINISTERED/HELD/REFUSED/PATIENT_NOT_AVAILABLE

        // Populated mainly for ADMINISTERED; optional overrides of the order line's own Dose/Route
        // (e.g. a partial dose given, or route changed clinically) — null means "as ordered".
        public string? AdministeredDose { get; set; }
        public string? AdministeredRoute { get; set; }
        public string? AdministrationSite { get; set; }

        // Required (non-empty) when ActionStatus is HELD/REFUSED/PATIENT_NOT_AVAILABLE.
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        // 5-Rights confirmation — the UI must have shown the checklist and the nurse ticked it;
        // this is a procedural/audit flag, not independently machine-verified (no barcode/scanner
        // hardware in this codebase). Rejected server-side if false.
        public bool FiveRightsConfirmed { get; set; }

        // High-alert witness co-sign — required (and validated server-side) only when the order
        // line's IsHighAlert is true. WitnessUserId is optional (the witness may not have a
        // system login this shift) but WitnessName is mandatory whenever witness is required,
        // mirroring the existing CK_MA_Witness DB check.
        public string? WitnessName { get; set; }
        public Guid? WitnessUserId { get; set; }
    }
}
