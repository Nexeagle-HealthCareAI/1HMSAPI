using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DeletePreferredMedicineRequestModel : IRequest<DeletePreferredMedicineResponseModel>
    {
       public long PreferredId { get; set; }
       public Guid DoctorId { get; set; }
       public Guid HospitalId { get; set; }
    }
}
