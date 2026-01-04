using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPersonalizedDataRequestModel : IRequest<UpsertPersonalizedDataResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? LookupType { get; set; }
        public string? Source  { get; set; }
        [JsonIgnore]
        public Guid LoggedInUserId { get; set; }
        public PersonalizedLookupDataModel Data { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class PersonalizedLookupDataModel
    {
        public string? PersonalId { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? ShortDesc { get; set; }
        public string? Synonyms { get; set; }
    }
}
