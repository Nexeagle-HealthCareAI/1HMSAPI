using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class AdviseAdmissionRequestModel : IRequest<AdviseAdmissionResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid ReferringDoctorId { get; set; }
        public Guid? AppointmentId { get; set; }
        public Guid? OtPlanId { get; set; }
        public Guid? PackageTypeId { get; set; }
        public string? ProcedureName { get; set; }
        public DateTime? ProbableAdmissionDate { get; set; }
        public string? CaseType { get; set; }   // EMERGENCY / PLANNED / URGENT
        public string? Notes { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
