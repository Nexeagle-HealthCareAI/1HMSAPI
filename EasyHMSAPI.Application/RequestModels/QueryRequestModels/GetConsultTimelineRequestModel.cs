using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetConsultTimelineRequestModel : IRequest<GetConsultTimelineResponseModel>
    {
        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public string PatientId { get; set; } = string.Empty;

        [Required]
        public Guid DoctorId { get; set; }

        // Date of the appointment being booked/previewed. Defaults to today when omitted.
        public DateTime? TargetDate { get; set; }
    }
}
