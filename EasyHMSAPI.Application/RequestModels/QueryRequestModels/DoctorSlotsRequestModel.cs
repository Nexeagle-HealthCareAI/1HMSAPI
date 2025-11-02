using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorSlotsRequestModel : MediatR.IRequest<DoctorSlotsResponseModel>
    {
        public Guid DoctorId { get; set; }
        public DateTime SlotDate { get; set; }
    }
}
