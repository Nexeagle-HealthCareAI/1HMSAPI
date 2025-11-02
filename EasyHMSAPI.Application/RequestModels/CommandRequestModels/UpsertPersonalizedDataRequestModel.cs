using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPersonalizedDataRequestModel : IRequest<UpsertPersonalizedDataResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string LookupType { get; set; } = string.Empty;
        public PersonalizedLookupDataModel Data { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class PersonalizedLookupDataModel
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? ShortDesc { get; set; }
        public string? Synonyms { get; set; }
    }
}
