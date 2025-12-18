using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientLookupDataResponseModel : IRequest<GetPatientLookupDataResponseModel>
    {
        public Guid? HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public string? LookupType { get; set; }
        public int? TotalTypes { get; set; }
        public List<LookIpDetailsDataModel>? Items { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class LookIpDetailsDataModel
    {
        public int LookupTypeId { get; set; }
        public string? LookupType { get; set; }
        public long Count { get; set; }
        public List<LookupPersonalDataModel>? PersonalData { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class LookupPersonalDataModel
    {
        public Guid PersonalId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? NameLower { get; set; }
        public string? ShortDesc { get; set; }
        public long UsageCount { get; set; }
    }
}
