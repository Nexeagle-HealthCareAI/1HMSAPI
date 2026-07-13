using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Platform-wide — returns doctors across every publicly-listed hospital, not one
    // hospital scoped by an API key. No fields: nothing is client-scoped.
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorsRequestModel : IRequest<GetPublicDoctorsResponseModel>
    {
    }
}
