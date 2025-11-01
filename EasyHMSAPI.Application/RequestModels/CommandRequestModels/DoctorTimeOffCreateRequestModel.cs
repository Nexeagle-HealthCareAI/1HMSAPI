using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DoctorTimeOffCreateRequestModel : MediatR.IRequest<DoctorTimeOffCreateResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Reason { get; set; }
    }
}
