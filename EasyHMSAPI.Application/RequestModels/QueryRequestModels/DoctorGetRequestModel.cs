using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorGetRequestModel : MediatR.IRequest<DoctorGetResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
