using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetPatientAppointmentDetailsRequestModel : IRequest<GetPatientAppointmentDetailsResponseModel>
    {
        public string? Status { get; set; }
        
        public DateTime? StartDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        [Required]
        public Guid HospitalId { get; set; }
        
        public Guid? DoctorId { get; set; }
    }
}
