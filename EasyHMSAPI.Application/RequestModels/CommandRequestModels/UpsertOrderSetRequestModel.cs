using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Create-or-update an OrderSet (nullable OrderSetId convention, same as UpsertPackageType).
    // Category is always "POST_OP" today -- the field exists for future set types, not exposed
    // as a picker in the v1 admin UI, so the frontend never sends anything else.
    [ExcludeFromCodeCoverage]
    public class UpsertOrderSetRequestModel : IRequest<UpsertOrderSetResponseModel>
    {
        public Guid? OrderSetId { get; set; }
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public List<OrderSetLineInput>? Lines { get; set; }
        public bool IsActive { get; set; } = true;
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }

    // A template line inside an OrderSet -- expanded into a real ClinicalOrderLineInput at apply
    // time (see PostOpOrderSetDialog on the frontend). No ChargeId/Urgency/ScheduledAt: those are
    // either not stable enough to template (a ChargeMaster item) or only meaningful when the
    // order is actually placed, not when the protocol is authored.
    [ExcludeFromCodeCoverage]
    public class OrderSetLineInput
    {
        public string ItemName { get; set; } = null!;
        public string OrderType { get; set; } = null!;
        public string? SaltName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public int? DurationDays { get; set; }
        public string? Instructions { get; set; }
        public bool IsHighAlert { get; set; }
        public decimal Qty { get; set; } = 1;
    }
}
