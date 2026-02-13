using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeletePreferredMedicineRequestModel : IRequest<DeletePreferredMedicineResponseModel>
    {
       public long PreferredId { get; set; }
       public Guid DoctorId { get; set; }
       public Guid HospitalId { get; set; }
    }
}
