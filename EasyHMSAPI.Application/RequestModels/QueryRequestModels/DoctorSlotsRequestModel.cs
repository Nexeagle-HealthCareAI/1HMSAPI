using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class DoctorSlotsRequestModel : MediatR.IRequest<DoctorSlotsResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DateTime SlotDate { get; set; }
    }
}
