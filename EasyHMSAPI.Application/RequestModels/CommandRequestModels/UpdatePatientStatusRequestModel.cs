using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class UpdatePatientStatusRequestModel : IRequest<UpdatePatientStatusResponseModel>
    {
        [Required]
        [JsonIgnore]
        public Guid UserId { get; set; }
        [Required]
        public Guid AppointmentId { get; set; }

        [Required]
        public string? PatientId { get; set; }

        [Required]
        [StringLength(50)]
        public string? CurrentStatus { get; set; }

        [Required]
        [StringLength(50)]
        public string? ToStatus { get; set; }

        public string? Reason { get; set; }
    }
}
