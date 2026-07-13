using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorsRequestModel : IRequest<GetPublicDoctorsResponseModel>
    {
        // Resolved by PublicApiKeyFilter from the caller's API key — never client-supplied.
        [JsonIgnore]
        public Guid HospitalId { get; set; }
    }
}
