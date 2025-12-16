using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class LookupPersonal
    {
        public Guid PersonalId { get; set; }
        public Guid HospitalID { get; set; }
        public Guid DoctorID { get; set; }
        public Guid? MasterLookupId { get; set; }
        public int LookupTypeId { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = null!;
        public string? NameLower { get; set; }
        public string? ShortDesc { get; set; }
        public string? MetaJson { get; set; }
        public bool IsActive { get; set; }
        public bool IsOverride { get; set; }
        public bool HideMaster { get; set; }
        public long UsageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime ModifiedAt { get; set; }
        public Guid? ModifiedBy { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public LookupType? LookupType { get; set; }
        public LookupMaster? MasterLookup { get; set; }
    }
}
