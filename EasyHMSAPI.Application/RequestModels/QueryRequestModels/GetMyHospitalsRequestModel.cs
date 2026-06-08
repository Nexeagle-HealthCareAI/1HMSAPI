using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    /// <summary>All hospitals the user belongs to (across any chain) — drives the hospital switcher.</summary>
    [ExcludeFromCodeCoverage]
    public class GetMyHospitalsRequestModel : IRequest<GetMyHospitalsResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
