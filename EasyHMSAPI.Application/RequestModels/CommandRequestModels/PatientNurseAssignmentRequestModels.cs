using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Assigns a nurse to a specific patient for a shift. ShiftDate null = a standing assignment
    // ("covers this patient's shift until released"); a real date = a one-off cover for that IST
    // calendar date. Team model, same as the ward roster: multiple different nurses can each hold
    // their own ACTIVE row for the same admission+shift+date -- UX_PNA_ActiveAssignment only stops
    // the SAME nurse being double-assigned. Independent of NurseShiftAssignment -- the nurse does
    // not need to already be on the ward roster.
    [ExcludeFromCodeCoverage]
    public class AssignPatientNurseRequestModel : IRequest<AssignPatientNurseResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid NurseUserId { get; set; }
        public Guid AdmissionId { get; set; }
        public string ShiftCode { get; set; } = null!;   // MORNING / EVENING / NIGHT
        public DateTime? ShiftDate { get; set; }
        public string? Notes { get; set; }
    }

    // Flips one ACTIVE patient assignment row to RELEASED, stamping UnassignedAt/By.
    [ExcludeFromCodeCoverage]
    public class ReleasePatientNurseRequestModel : IRequest<ReleasePatientNurseResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid PatientNurseAssignmentId { get; set; }
    }
}
