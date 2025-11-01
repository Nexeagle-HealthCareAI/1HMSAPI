namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class LookupTypeInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ScopeInfo
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
    }

    public class LookupItemPersonal
    {
        public Guid PersonalId { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameLower { get; set; }
        public string? ShortDesc { get; set; }
        public long UsageCount { get; set; }
    }

    public class LookupItemGeneral
    {
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameLower { get; set; }
        public string? ShortDesc { get; set; }
        public List<string>? Synonyms { get; set; }
        public long UsageCount { get; set; }
    }

    public class GetPatientLookupDataResponseModel
    {
        public LookupTypeInfo LookupType { get; set; } = new LookupTypeInfo();
        public ScopeInfo Scope { get; set; } = new ScopeInfo();
        public (int personal, int general) Counts { get; set; }
        public List<LookupItemPersonal> PersonalItems { get; set; } = new List<LookupItemPersonal>();
        public List<LookupItemGeneral> GeneralItems { get; set; } = new List<LookupItemGeneral>();
    }
}
