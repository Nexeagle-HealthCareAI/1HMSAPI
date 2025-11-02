using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorOverrideDeleteRequestModel : MediatR.IRequest<DoctorOverrideDeleteResponseModel>
    {
        public Guid OverrideId { get; set; }
    }
}
