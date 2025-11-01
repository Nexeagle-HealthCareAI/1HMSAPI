using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class DoctorShiftConfigRequestModel : MediatR.IRequest<DoctorShiftConfigResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DateTime StartDate { get; set; }
        public int? DaysCount { get; set; }
    }
}
