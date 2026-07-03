using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetConsentTemplatesRequestModel : IRequest<GetConsentTemplatesResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? TypeCode { get; set; }
        public string? Language { get; set; }
        public bool ActiveOnly { get; set; } = true;
    }

    [ExcludeFromCodeCoverage]
    public class GetConsentRecordsRequestModel : IRequest<GetConsentRecordsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
    }
}
