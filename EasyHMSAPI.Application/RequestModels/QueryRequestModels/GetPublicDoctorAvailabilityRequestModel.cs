using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorAvailabilityRequestModel : IRequest<GetPublicDoctorAvailabilityResponseModel>
    {
        // Resolved by PublicApiKeyFilter from the caller's API key — never client-supplied.
        [JsonIgnore]
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public DateTime Date { get; set; }
    }
}
