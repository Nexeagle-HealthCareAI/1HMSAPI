using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class CreatePathologyOrderRequestModel : IRequest<CreatePathologyOrderResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = null!;
        public Guid? EncounterId { get; set; }
        public Guid? AdmissionId { get; set; }
        public Guid? OrderedByDoctorId { get; set; }
        public string? Notes { get; set; }
        public List<Guid> TestIds { get; set; } = new();

        // OPD, IPD, EMERGENCY, WALK_IN -- see PathologyOrder.SourceType. Defaults to OPD when the
        // caller omits it (every existing caller predates this field).
        public string? SourceType { get; set; }
        public bool IsStat { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        
        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
    }
}
