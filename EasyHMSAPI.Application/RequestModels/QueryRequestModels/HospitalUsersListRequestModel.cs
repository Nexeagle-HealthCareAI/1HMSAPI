using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class HospitalUsersListRequestModel : MediatR.IRequest<HospitalUsersListResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
