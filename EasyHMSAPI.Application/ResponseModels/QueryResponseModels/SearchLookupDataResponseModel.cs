using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SearchLookupDataResponseModel
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? LookupType { get; set; }
        public int? LookupTypeId { get; set; }
        public List<PersonalLookupDataModel>? PersonalLookupData { get; set; }
        public List<MasterLookupDataModel>? MasterLookupData { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PersonalLookupDataModel
    {
        public Guid PersonalId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? NameLower { get; set; }
        public string? ShortDesc { get; set; }
        public long UsageCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class  MasterLookupDataModel
    {
        public Guid LookupId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? NameLower { get; set; }
        public string? ShortDesc { get; set; }
        public long UsageCount { get; set; }
    }
}
