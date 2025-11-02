using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorTimeOffDeleteRequestModel : MediatR.IRequest<DoctorTimeOffDeleteResponseModel>
    {
        public Guid TimeOffId { get; set; }
    }
}
