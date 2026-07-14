using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class LogInfectionEventRequestModel : IRequest<LogInfectionEventResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public Guid? DeviceAssignmentId { get; set; }
        public string InfectionType { get; set; } = null!;
        public DateTime? DiagnosedAt { get; set; }
        public string DiagnosedByDoctorName { get; set; } = null!;
        public string? CultureOrganism { get; set; }
        public string? Notes { get; set; }
    }
}
