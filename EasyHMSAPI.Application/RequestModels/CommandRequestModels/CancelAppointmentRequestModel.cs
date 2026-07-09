using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CancelAppointmentRequestModel : IRequest<CancelAppointmentResponseModel>
    {
        [Required]
        public Guid AppointmentId { get; set; }

        [Required]
        public string? PatientId { get; set; }

        [Required]
        public Guid HospitalId { get; set; }

        public string? Reason { get; set; }

        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
