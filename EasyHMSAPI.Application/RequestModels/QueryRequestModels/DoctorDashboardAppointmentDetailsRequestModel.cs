using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class DoctorDashboardAppointmentDetailsRequestModel : IRequest<DoctorDashboardAppointmentDetailsResponseModel>
    {
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
    }
}
