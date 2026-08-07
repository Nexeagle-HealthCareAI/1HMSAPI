using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Rosters a nurse onto a ward for a shift. ShiftDate null = a standing assignment ("covers this
    // ward/shift until released"); a real date = a one-off cover for that IST calendar date. Team
    // model: multiple different nurses can each hold their own ACTIVE row for the same
    // ward+shift+date -- UX_NSA_ActiveRoster only stops the SAME nurse being double-booked.
    [ExcludeFromCodeCoverage]
    public class AssignNurseShiftRequestModel : IRequest<AssignNurseShiftResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid NurseUserId { get; set; }
        public string WardCode { get; set; } = null!;
        public string ShiftCode { get; set; } = null!;   // MORNING / EVENING / NIGHT
        public DateTime? ShiftDate { get; set; }
        public string? Notes { get; set; }
    }

    // Flips one ACTIVE roster row to RELEASED, stamping UnassignedAt/By -- the audit trail for
    // "this nurse is done covering this ward/shift."
    [ExcludeFromCodeCoverage]
    public class ReleaseNurseShiftRequestModel : IRequest<ReleaseNurseShiftResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid NurseShiftAssignmentId { get; set; }
    }
}
