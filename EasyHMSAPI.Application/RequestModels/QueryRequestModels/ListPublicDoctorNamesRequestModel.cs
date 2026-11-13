using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // DoctorId + name of every doctor live on Doctor Dekho, for Vita's in-memory name index (it
    // spots "Dr Sharma"-style asks without a round trip). Optional City narrows by hospital city.
    [ExcludeFromCodeCoverage]
    public class ListPublicDoctorNamesRequestModel : IRequest<ListPublicDoctorNamesResponseModel>
    {
        public string? City { get; set; }
    }
}
