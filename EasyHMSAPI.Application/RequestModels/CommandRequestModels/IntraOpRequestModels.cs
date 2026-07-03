using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert — one IntraOpRecord row per SurgeryCase.
    [ExcludeFromCodeCoverage]
    public class SaveIntraOpRecordRequestModel : IRequest<SaveIntraOpRecordResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public string? AnaesthesiaType { get; set; }
        public DateTime? AnaesthesiaStartAt { get; set; }
        public DateTime? AnaesthesiaEndAt { get; set; }
        public DateTime? SurgeryStartAt { get; set; }
        public DateTime? SurgeryEndAt { get; set; }
        public decimal? EstimatedBloodLossMl { get; set; }
        public string? Findings { get; set; }
        public string? ProcedurePerformed { get; set; }
        public string? SurgicalTeam { get; set; }
        public string? ComplicationsNotes { get; set; }
    }

    // Records one item actually used during surgery (descriptive, after-the-fact — distinct from
    // CPOE's prescriptive ClinicalOrder). Optionally drives an InventoryMovement stock deduction
    // (InventoryItemId set) and/or a billing charge event (ChargeId set); Category=IMPLANT rows
    // with LotNumber/SerialNumber double as CSSD's implant traceability log.
    [ExcludeFromCodeCoverage]
    public class RecordIntraOpItemUsageRequestModel : IRequest<RecordIntraOpItemUsageResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string Category { get; set; } = null!;
        public decimal Qty { get; set; }
        public string? LotNumber { get; set; }
        public string? SerialNumber { get; set; }
        public Guid? ChargeId { get; set; }
        public decimal? UnitRate { get; set; }
    }
}
